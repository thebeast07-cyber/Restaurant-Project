using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Organization;
using Restaurant.Domain.Purchasing;

namespace Restaurant.Tests;

/// <summary>
/// RecordPayment's overpay guard (AmountPaid + Amount <= TotalAmount, checked
/// atomically) needs the same stress test discipline as every other conditional
/// UPDATE in this codebase — verifies concurrent partial payments against one
/// Purchase can never together exceed its TotalAmount, even though each individual
/// request's application-level view of "remaining balance" could be stale.
/// </summary>
public class PurchasePaymentRaceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PurchasePaymentRaceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentPayments_OnSamePurchase_NeverExceedsTotalAmount()
    {
        const int concurrentAttempts = 20;
        const decimal paymentAmount = 10m;
        const decimal totalAmount = 100m; // exactly 10 payments of 10 should succeed, not 20

        var (supplierId, ingredientId) = await SeedSupplierAndIngredientAsync();

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("owner", "Owner#12345");
        var setupClient = _factory.CreateClient().WithToken(token);

        var purchaseResponse = await setupClient.CreatePurchaseAsync(supplierId, ingredientId, quantity: totalAmount, unit: "pcs", unitCost: 1m);
        purchaseResponse.EnsureSuccessStatusCode();
        var purchase = await purchaseResponse.Content.ReadFromJsonAsync<PurchaseDto>();

        var clients = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => _factory.CreateClient().WithToken(token))
            .ToList();

        var tasks = clients.Select(c => c.RecordPurchasePaymentAsync(purchase!.Id, paymentAmount));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(
            (int)r.StatusCode is 200 or 400,
            $"Unexpected status {r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));

        var successCount = results.Count(r => (int)r.StatusCode == 200);
        Assert.Equal((int)(totalAmount / paymentAmount), successCount);

        await using var db = _factory.CreateDbContext();
        var finalPurchase = await db.Purchases.IgnoreQueryFilters().SingleAsync(p => p.Id == purchase!.Id);
        Assert.Equal(totalAmount, finalPurchase.AmountPaid);
        Assert.Equal(PurchasePaymentStatus.Paid, finalPurchase.PaymentStatus);

        var paymentRowCount = await db.PurchasePayments.IgnoreQueryFilters().CountAsync(p => p.PurchaseId == purchase!.Id);
        Assert.Equal(successCount, paymentRowCount);
    }

    private async Task<(Guid supplierId, Guid ingredientId)> SeedSupplierAndIngredientAsync()
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var supplier = new Supplier { TenantId = tenant.Id, BranchId = branch.Id, Name = $"PaymentRaceSupplier-{Guid.NewGuid():N}" };
        var ingredient = new Restaurant.Domain.Catalog.Ingredient
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = $"PaymentRaceIngredient-{Guid.NewGuid():N}",
            Unit = "pcs"
        };
        db.Suppliers.Add(supplier);
        db.Ingredients.Add(ingredient);
        db.Stocks.Add(new Restaurant.Domain.Inventory.Stock { TenantId = tenant.Id, BranchId = branch.Id, IngredientId = ingredient.Id, Quantity = 0 });

        await db.SaveChangesAsync();
        return (supplier.Id, ingredient.Id);
    }

    private record PurchaseDto(Guid Id);
}
