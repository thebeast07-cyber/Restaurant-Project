using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Sales;

namespace Restaurant.Tests;

/// <summary>
/// Targets a different race than StockRaceConditionTests: not "many orders competing
/// for scarce stock", but "the SAME order checked out twice at once" (double-click,
/// duplicate request, retried client). The fixed stock deduction (atomic per
/// Ingredient) says nothing about whether two concurrent checkout calls on one Order
/// can both pass the "is this order still payable" check before either commits —
/// that's a second, independent race condition to verify.
/// </summary>
public class OrderCheckoutRaceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OrderCheckoutRaceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentCheckouts_OnSameOrder_OnlyOneSucceeds()
    {
        const int concurrentAttempts = 10;

        var (productId, ingredientId) = await SeedPlentifulProductAsync(quantity: 100_000m);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("owner", "Owner#12345");
        var setupClient = _factory.CreateClient().WithToken(token);
        await setupClient.EnsureShiftOpenAsync();

        var orderId = await setupClient.CreateOrderAsync();
        var addResponse = await setupClient.AddItemAsync(orderId, productId, quantity: 1);
        addResponse.EnsureSuccessStatusCode();

        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();

        // All fired at the SAME order — this is the whole point.
        var tasks = clients.Select(client => client.CheckoutAsync(orderId));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 400,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);
        Assert.True(successCount == 1,
            $"Expected exactly 1 successful checkout on a single order, got {successCount}.");

        await using var db = _factory.CreateDbContext();

        var paymentCount = await db.Payments.IgnoreQueryFilters().CountAsync(p => p.OrderId == orderId);
        Assert.Equal(1, paymentCount);

        var journalCount = await db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.ReferenceType == "Order" && j.ReferenceId == orderId);
        Assert.Equal(1, journalCount);

        var stockMovementCount = await db.StockMovements.IgnoreQueryFilters()
            .CountAsync(m => m.IngredientId == ingredientId && m.ReferenceId == orderId);
        Assert.Equal(1, stockMovementCount);
    }

    private async Task<(Guid productId, Guid ingredientId)> SeedPlentifulProductAsync(decimal quantity)
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var category = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = $"RaceTest-{Guid.NewGuid():N}" };
        var ingredient = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = $"RaceIngredient-{Guid.NewGuid():N}", Unit = "pcs" };
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);

        var product = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = category.Id,
            Name = $"RaceProduct-{Guid.NewGuid():N}",
            Price = 1000m,
            Station = Station.Kitchen
        };
        product.RecipeItems.Add(new RecipeItem
        {
            TenantId = tenant.Id,
            ProductId = product.Id,
            IngredientId = ingredient.Id,
            Quantity = 1,
            Unit = "pcs"
        });
        db.Products.Add(product);

        db.Stocks.Add(new Stock { TenantId = tenant.Id, BranchId = branch.Id, IngredientId = ingredient.Id, Quantity = quantity });

        await db.SaveChangesAsync();
        return (product.Id, ingredient.Id);
    }
}
