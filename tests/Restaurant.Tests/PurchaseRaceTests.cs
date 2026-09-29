using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Organization;
using Restaurant.Domain.Purchasing;

namespace Restaurant.Tests;

/// <summary>
/// Recording a Purchase increments Stock via the same "conditional increment, no
/// compare-and-swap needed" pattern as ManualAdjustment (StockAdjustmentRaceTests) —
/// correct by construction under concurrency, but every atomic-increment path in this
/// codebase gets its own stress test rather than being assumed safe by analogy.
/// </summary>
public class PurchaseRaceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PurchaseRaceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentPurchases_OnSameIngredient_NeverLosesAnUpdate()
    {
        const int concurrentAttempts = 15;
        var (supplierId, ingredientId) = await SeedSupplierAndIngredientAsync(startingQuantity: 100m);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("owner", "Owner#12345");
        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();

        // Every request buys +2kg — if the increment ever lost an update, the final
        // quantity would be short of starting + (2 * concurrentAttempts).
        var tasks = clients.Select(c => c.CreatePurchaseAsync(supplierId, ingredientId, quantity: 2m, unit: "kg", unitCost: 10000m));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True((int)r.StatusCode == 200,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        await using var db = _factory.CreateDbContext();
        var finalStock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.Equal(100m + 2m * concurrentAttempts, finalStock.Quantity);

        var purchaseCount = await db.Purchases.IgnoreQueryFilters().CountAsync(p => p.SupplierId == supplierId);
        Assert.Equal(concurrentAttempts, purchaseCount);

        var journalCount = await db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.ReferenceType == "Purchase");
        // At least concurrentAttempts (other tests in the suite may also post
        // Purchase journal entries against other ingredients/suppliers) — the precise
        // invariant that matters here is 1 journal entry per successful Purchase.
        Assert.True(journalCount >= concurrentAttempts);
    }

    private async Task<(Guid supplierId, Guid ingredientId)> SeedSupplierAndIngredientAsync(decimal startingQuantity)
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var supplier = new Supplier { TenantId = tenant.Id, BranchId = branch.Id, Name = $"RaceSupplier-{Guid.NewGuid():N}" };
        var ingredient = new Restaurant.Domain.Catalog.Ingredient
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = $"PurchaseRaceIngredient-{Guid.NewGuid():N}",
            Unit = "kg"
        };
        db.Suppliers.Add(supplier);
        db.Ingredients.Add(ingredient);
        db.Stocks.Add(new Restaurant.Domain.Inventory.Stock
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            IngredientId = ingredient.Id,
            Quantity = startingQuantity
        });

        await db.SaveChangesAsync();
        return (supplier.Id, ingredient.Id);
    }
}
