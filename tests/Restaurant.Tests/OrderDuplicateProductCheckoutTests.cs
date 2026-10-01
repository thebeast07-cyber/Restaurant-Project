using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Sales;

namespace Restaurant.Tests;

/// <summary>
/// AddItem always inserts a new OrderItem row rather than merging into an existing
/// line for the same product, so an order can legitimately have two or more
/// OrderItem rows with the same ProductId (e.g. the same product added twice from
/// the Order page UI). Checkout must aggregate quantity per product instead of
/// assuming ProductId is unique across an order's items.
/// </summary>
public class OrderDuplicateProductCheckoutTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OrderDuplicateProductCheckoutTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_WithSameProductAddedTwice_Succeeds()
    {
        var (productId, ingredientId) = await SeedPlentifulProductAsync(quantity: 100m);

        var loginClient = _factory.CreateClient();
        var token = await loginClient.LoginAsync("owner", "Owner#12345");
        var client = _factory.CreateClient().WithToken(token);
        await client.EnsureShiftOpenAsync();

        var orderId = await client.CreateOrderAsync();
        (await client.AddItemAsync(orderId, productId, quantity: 1)).EnsureSuccessStatusCode();
        (await client.AddItemAsync(orderId, productId, quantity: 1)).EnsureSuccessStatusCode();

        var checkoutResponse = await client.CheckoutAsync(orderId);

        Assert.True(checkoutResponse.IsSuccessStatusCode,
            $"Checkout failed with {checkoutResponse.StatusCode}: {await checkoutResponse.Content.ReadAsStringAsync()}");

        await using var db = _factory.CreateDbContext();

        var stock = await db.Stocks.IgnoreQueryFilters().SingleAsync(s => s.IngredientId == ingredientId);
        Assert.Equal(98m, stock.Quantity);
    }

    private async Task<(Guid productId, Guid ingredientId)> SeedPlentifulProductAsync(decimal quantity)
    {
        await using var db = _factory.CreateDbContext();
        var tenant = await db.Tenants.SingleAsync();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.TenantId == tenant.Id);

        var category = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = $"DupTest-{Guid.NewGuid():N}" };
        var ingredient = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = $"DupIngredient-{Guid.NewGuid():N}", Unit = "pcs" };
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);

        var product = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = category.Id,
            Name = $"DupProduct-{Guid.NewGuid():N}",
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
