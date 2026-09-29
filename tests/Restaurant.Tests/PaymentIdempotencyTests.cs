using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;

namespace Restaurant.Tests;

/// <summary>
/// The Day 13 sprint's "idempotency payment" scenario, tested as a SEQUENTIAL retry
/// (client times out waiting for the first response and retries the same checkout
/// call after it already landed) — the complementary case to
/// OrderCheckoutRaceTests, which covers the CONCURRENT double-click instead.
/// Both hit the same atomic Order-status claim in OrdersController.Checkout, but a
/// sequential retry is worth its own test: it's the more common real-world failure
/// mode for a POS on a flaky network, and it should never be tempting to add a
/// separate idempotency-key mechanism here since the existing claim already makes a
/// retried checkout safe.
/// </summary>
public class PaymentIdempotencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PaymentIdempotencyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RetryingCheckoutAfterSuccess_FailsCleanly_WithoutDuplicatingPaymentOrJournalOrStock()
    {
        var (productId, ingredientId) = await SeedProductAsync(stock: 100m);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("cashier", "Cashier#12345");
        var client = _factory.CreateClient().WithToken(token);
        await client.EnsureShiftOpenAsync();

        var orderId = await client.CreateOrderAsync();
        (await client.AddItemAsync(orderId, productId, quantity: 1)).EnsureSuccessStatusCode();

        var first = await client.CheckoutAsync(orderId);
        Assert.Equal(System.Net.HttpStatusCode.OK, first.StatusCode);

        // Simulates a client that never saw the first response (timeout, dropped
        // connection) and retries the exact same request afterward.
        var retry = await client.CheckoutAsync(orderId);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, retry.StatusCode);

        await using var db = _factory.CreateDbContext();

        var paymentCount = await db.Payments.IgnoreQueryFilters().CountAsync(p => p.OrderId == orderId);
        Assert.Equal(1, paymentCount);

        var journalCount = await db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.ReferenceType == "Order" && j.ReferenceId == orderId);
        Assert.Equal(1, journalCount);

        var stock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.Equal(99m, stock.Quantity);
    }

    private async Task<(Guid productId, Guid ingredientId)> SeedProductAsync(decimal stock)
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var category = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = $"IdempotencyTest-{Guid.NewGuid():N}" };
        var ingredient = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = $"IdempotencyIngredient-{Guid.NewGuid():N}", Unit = "pcs" };
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);

        var product = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = category.Id,
            Name = $"IdempotencyProduct-{Guid.NewGuid():N}",
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

        db.Stocks.Add(new Stock { TenantId = tenant.Id, BranchId = branch.Id, IngredientId = ingredient.Id, Quantity = stock });

        await db.SaveChangesAsync();
        return (product.Id, ingredient.Id);
    }
}
