using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Organization;
using Restaurant.Domain.Sales;

namespace Restaurant.Tests;

/// <summary>
/// Same concurrency class as the Shift/Stock/Order-checkout races already fixed: two
/// parties can't both be seated at the same table at once. Verifies the atomic
/// Available->Occupied claim in OrdersController.Create actually holds under real
/// concurrency, not just "looks right reading the code".
/// </summary>
public class TableClaimRaceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TableClaimRaceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentOrderCreation_OnSameTable_OnlyOneSucceeds()
    {
        const int concurrentAttempts = 15;

        var tableId = await SeedFreshTableAsync();

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("manager", "Manager#12345");

        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();
        await clients[0].EnsureShiftOpenAsync();

        var tasks = clients.Select(client => client.CreateOrderRawAsync(tableId));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 400,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);
        Assert.True(successCount == 1,
            $"Expected exactly 1 successful order creation for one table, got {successCount}.");

        await using var db = _factory.CreateDbContext();
        var table = await db.Tables.IgnoreQueryFilters().SingleAsync(t => t.Id == tableId);
        Assert.Equal(TableStatus.Occupied, table.Status);
    }

    private async Task<Guid> SeedFreshTableAsync()
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var table = new RestaurantTable
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Number = $"RaceTest-{Guid.NewGuid():N}"
        };
        db.Tables.Add(table);
        await db.SaveChangesAsync();
        return table.Id;
    }
}
