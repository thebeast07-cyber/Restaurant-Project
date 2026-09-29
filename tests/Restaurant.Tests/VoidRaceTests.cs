using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;

namespace Restaurant.Tests;

/// <summary>
/// Same concurrency class as OrderCheckoutRaceTests, applied to Void: two concurrent
/// void attempts on the same Completed order must not both succeed (would double-
/// restore stock and post two reversal journals for one sale).
/// </summary>
public class VoidRaceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public VoidRaceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentVoids_OnSameOrder_OnlyOneSucceeds()
    {
        const int concurrentAttempts = 10;

        var (productId, ingredientId) = await SeedPlentifulProductAsync(quantity: 100_000m);

        var loginClient = _factory.CreateClient();
        var managerToken = await loginClient.LoginAsync("manager", "Manager#12345");
        var setupClient = _factory.CreateClient().WithToken(managerToken);
        await setupClient.EnsureShiftOpenAsync();

        var orderId = await setupClient.CreateOrderAsync();
        var addResponse = await setupClient.AddItemAsync(orderId, productId, quantity: 5);
        addResponse.EnsureSuccessStatusCode();
        var checkoutResponse = await setupClient.CheckoutAsync(orderId);
        checkoutResponse.EnsureSuccessStatusCode();

        await using (var setupDb = _factory.CreateDbContext())
        {
            var stockAfterSale = await setupDb.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
            Assert.Equal(100_000m - 5, stockAfterSale.Quantity);
        }

        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(managerToken))
            .ToList();

        var tasks = clients.Select(client => client.VoidOrderAsync(orderId, "2222", "stress test"));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 400,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);
        Assert.True(successCount == 1, $"Expected exactly 1 successful void, got {successCount}.");

        await using var db = _factory.CreateDbContext();

        var finalStock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.Equal(100_000m, finalStock.Quantity); // restored exactly, not over/under

        var reversalCount = await db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.ReferenceType == "Order" && j.ReferenceId == orderId && j.IsReversal);
        Assert.Equal(1, reversalCount);

        var voidMovementCount = await db.StockMovements.IgnoreQueryFilters()
            .CountAsync(m => m.IngredientId == ingredientId && m.ReferenceId == orderId && m.Reason == StockMovementReason.Void);
        Assert.Equal(1, voidMovementCount);
    }

    private async Task<(Guid productId, Guid ingredientId)> SeedPlentifulProductAsync(decimal quantity)
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var category = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = $"VoidRaceTest-{Guid.NewGuid():N}" };
        var ingredient = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = $"VoidRaceIngredient-{Guid.NewGuid():N}", Unit = "pcs" };
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);

        var product = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = category.Id,
            Name = $"VoidRaceProduct-{Guid.NewGuid():N}",
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
