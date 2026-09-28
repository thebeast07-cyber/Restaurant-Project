using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Tests;

/// <summary>
/// Normal-load counterpart to StockRaceConditionTests: no artificial scarcity, just
/// many genuinely concurrent checkouts against plentiful stock. This catches a
/// different failure mode — even when nothing SHOULD be rejected, a lost-update race
/// can silently under-deduct (final stock too high) without ever tripping the
/// shortage path. Checking exact final quantity is the point, not just "no crash".
/// </summary>
public class CheckoutLoadTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CheckoutLoadTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentCheckouts_OnPlentifulStock_AllSucceedAndStockIsExact()
    {
        const decimal initialStock = 100_000m;
        const int concurrentOrders = 50;
        const int quantityPerOrder = 3;

        var (productId, ingredientId) = await SeedPlentifulProductAsync(initialStock);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("manager", "Manager#12345");

        var clients = Enumerable.Range(0, concurrentOrders)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();
        await clients[0].EnsureShiftOpenAsync();

        var tasks = clients.Select(async client =>
        {
            var orderId = await client.CreateOrderAsync();
            var addResponse = await client.AddItemAsync(orderId, productId, quantityPerOrder);
            addResponse.EnsureSuccessStatusCode();
            var checkoutResponse = await client.CheckoutAsync(orderId);
            return (orderId, checkoutResponse);
        });

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(
            r.checkoutResponse.IsSuccessStatusCode,
            $"Expected all checkouts to succeed with plentiful stock, got {r.checkoutResponse.StatusCode}: {r.checkoutResponse.Content.ReadAsStringAsync().Result}"));

        await using var db = _factory.CreateDbContext();
        var finalStock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        var expectedStock = initialStock - concurrentOrders * quantityPerOrder;

        // The number that actually matters: exact final quantity, not just "no error".
        // A lost update here would under-deduct silently — e.g. land on 99,910 instead
        // of 99,850 — with every individual request still reporting 200 OK.
        Assert.Equal(expectedStock, finalStock.Quantity);

        var movementCount = await db.StockMovements.IgnoreQueryFilters().CountAsync(m => m.IngredientId == ingredientId);
        Assert.Equal(concurrentOrders, movementCount);

        var orderIds = results.Select(r => r.orderId).ToList();
        var journalCount = await db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.ReferenceType == "Order" && orderIds.Contains(j.ReferenceId));
        Assert.Equal(concurrentOrders, journalCount);

        var paymentCount = await db.Payments.IgnoreQueryFilters().CountAsync(p => orderIds.Contains(p.OrderId));
        Assert.Equal(concurrentOrders, paymentCount);
    }

    private async Task<(Guid productId, Guid ingredientId)> SeedPlentifulProductAsync(decimal quantity)
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var category = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = $"LoadTest-{Guid.NewGuid():N}" };
        var ingredient = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = $"PlentifulIngredient-{Guid.NewGuid():N}", Unit = "pcs" };
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);

        var product = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = category.Id,
            Name = $"PlentifulProduct-{Guid.NewGuid():N}",
            Price = 500m,
            Station = Station.Bar
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
