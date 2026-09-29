using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Organization;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Tests;

/// <summary>
/// Verifies the fix applied after switching AdjustStock from read-then-write to an
/// atomic conditional UPDATE: concurrent ManualAdjustment deltas on the same
/// Ingredient must never lose an update, and concurrent Opname counts against a
/// changing Quantity must resolve to exactly one winner via compare-and-swap, not a
/// silently clobbered value.
/// </summary>
public class StockAdjustmentRaceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StockAdjustmentRaceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentManualAdjustments_OnSameIngredient_NeverLosesAnUpdate()
    {
        const int concurrentAttempts = 20;
        var ingredientId = await SeedIngredientAsync(startingQuantity: 1000);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("owner", "Owner#12345");
        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();

        // Every request applies +1 — if any read-then-write lost update survived, the
        // final quantity would be short of startingQuantity + concurrentAttempts.
        var tasks = clients.Select(c => c.AdjustStockAsync(ingredientId, deltaQuantity: 1));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True((int)r.StatusCode == 200,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        await using var db = _factory.CreateDbContext();
        var finalStock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.Equal(1000 + concurrentAttempts, finalStock.Quantity);

        var movementCount = await db.StockMovements.IgnoreQueryFilters()
            .CountAsync(m => m.IngredientId == ingredientId && m.Reason == StockMovementReason.ManualAdjustment);
        Assert.Equal(concurrentAttempts, movementCount);
    }

    [Fact]
    public async Task ConcurrentOpnameCounts_OnSameIngredient_ExactlyOneWinsAndOthersGetConflict()
    {
        const int concurrentAttempts = 10;
        var ingredientId = await SeedIngredientAsync(startingQuantity: 500);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("owner", "Owner#12345");
        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();

        // Each "counter" reports a different absolute figure for the same physical
        // count — only one compare-and-swap can win against the original Quantity.
        var tasks = clients.Select((c, i) => c.AdjustStockAsync(ingredientId, countedQuantity: 400 + i));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 409,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);
        Assert.Equal(1, successCount);

        await using var db = _factory.CreateDbContext();
        var finalStock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.NotEqual(500, finalStock.Quantity); // the single winning count was applied
    }

    private async Task<Guid> SeedIngredientAsync(decimal startingQuantity)
    {
        await using var db = _factory.CreateDbContext();

        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var ingredient = new Restaurant.Domain.Catalog.Ingredient
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = $"AdjustRaceIngredient-{Guid.NewGuid():N}",
            Unit = "pcs"
        };
        db.Ingredients.Add(ingredient);
        db.Stocks.Add(new Stock { TenantId = tenant.Id, BranchId = branch.Id, IngredientId = ingredient.Id, Quantity = startingQuantity });

        await db.SaveChangesAsync();

        return ingredient.Id;
    }
}
