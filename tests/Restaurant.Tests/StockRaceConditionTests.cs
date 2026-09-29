using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Organization;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Tests;

/// <summary>
/// Forces the exact scenario PRD §15 warns about: multiple simultaneous transactions
/// competing for the same last unit(s) of stock. Checkout currently reads
/// Stock.Quantity, computes a new value in memory, and writes it back — a classic
/// read-modify-write race. Under true concurrency this can silently produce a WRONG
/// final quantity (lost update) rather than a clean rejection, which is worse than a
/// crash because nothing looks wrong until someone reconciles stock later.
/// </summary>
public class StockRaceConditionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StockRaceConditionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentCheckouts_OnLimitedStock_NeverOversellsOrGoesNegative()
    {
        const int availableStock = 5;
        const int concurrentAttempts = 20;

        var (tenantId, branchId, productId, ingredientId) = await SeedScarceProductAsync(availableStock);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("cashier", "Cashier#12345");

        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();

        await clients[0].EnsureShiftOpenAsync();

        // Each client independently creates its own order, adds 1x the scarce product,
        // then checks out — all fired at once so they genuinely overlap in time rather
        // than running one after another.
        var tasks = clients.Select(async client =>
        {
            var orderId = await client.CreateOrderAsync();
            var addResponse = await client.AddItemAsync(orderId, productId, quantity: 1);
            addResponse.EnsureSuccessStatusCode();
            return await client.CheckoutAsync(orderId);
        });

        var results = await Task.WhenAll(tasks);

        // No request should ever crash (500) — insufficient stock is a clean, expected
        // 400, not an unhandled exception.
        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 400,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);

        await using var db = _factory.CreateDbContext();
        var finalStock = await db.Stocks
            .IgnoreQueryFilters()
            .SingleAsync(s => s.IngredientId == ingredientId);

        // The two invariants that actually matter: never oversell, never go negative.
        Assert.True(successCount <= availableStock,
            $"{successCount} checkouts succeeded but only {availableStock} were available — overselling occurred.");
        Assert.True(finalStock.Quantity >= 0,
            $"Stock went negative: {finalStock.Quantity}.");
        Assert.Equal(availableStock - successCount, finalStock.Quantity);
    }

    /// <summary>
    /// The literal Day 13 sprint scenario (docs/architecture/01-mvp-technical-design.md
    /// §4: "2 order rebutan item terakhir") as its own minimal, canonical repro,
    /// separate from the 20-vs-5 stress test above — exactly 1 unit of stock, exactly
    /// 2 orders, so a failure here points straight at the last-unit edge case rather
    /// than needing to be inferred from a larger scenario.
    /// </summary>
    [Fact]
    public async Task TwoOrders_CompetingForTheLastUnit_OnlyOneSucceeds()
    {
        var (_, _, productId, ingredientId) = await SeedScarceProductAsync(quantity: 1);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("cashier", "Cashier#12345");
        var clientA = _factory.CreateClient().WithToken(token);
        var clientB = _factory.CreateClient().WithToken(token);

        await clientA.EnsureShiftOpenAsync();

        var orderA = await clientA.CreateOrderAsync();
        var orderB = await clientB.CreateOrderAsync();
        (await clientA.AddItemAsync(orderA, productId, quantity: 1)).EnsureSuccessStatusCode();
        (await clientB.AddItemAsync(orderB, productId, quantity: 1)).EnsureSuccessStatusCode();

        // Fired together via Task.WhenAll, not two sequential awaits — they must
        // genuinely overlap for this to exercise the race instead of two serialized
        // requests that would trivially never conflict.
        var results = await Task.WhenAll(clientA.CheckoutAsync(orderA), clientB.CheckoutAsync(orderB));

        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 400,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);
        Assert.Equal(1, successCount);

        await using var db = _factory.CreateDbContext();
        var finalStock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.Equal(0, finalStock.Quantity);
    }

    private async Task<(Guid tenantId, Guid branchId, Guid productId, Guid ingredientId)> SeedScarceProductAsync(int quantity)
    {
        await using var db = _factory.CreateDbContext();

        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var category = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = $"StressTest-{Guid.NewGuid():N}" };
        var ingredient = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = $"ScarceIngredient-{Guid.NewGuid():N}", Unit = "pcs" };
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);

        var product = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = category.Id,
            Name = $"ScarceProduct-{Guid.NewGuid():N}",
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

        return (tenant.Id, branch.Id, product.Id, ingredient.Id);
    }
}
