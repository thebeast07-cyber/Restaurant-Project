using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Restaurant.Tests;

/// <summary>Thin helpers over HttpClient so stress tests read as business steps, not plumbing.</summary>
public static class TestApiClient
{
    public static async Task<string> LoginAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        return body!.Token;
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Opens a shift for the current token's user; tolerates "already open" (409).</summary>
    public static async Task EnsureShiftOpenAsync(this HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/shifts/open", new { openingCash = 100000m });
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public static async Task<Guid> CreateOrderAsync(this HttpClient client, Guid? tableId = null)
    {
        var response = await client.CreateOrderRawAsync(tableId);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OrderDto>();
        return body!.Id;
    }

    /// <summary>Raw response variant for tests that need to assert on failure status codes too.</summary>
    public static async Task<HttpResponseMessage> CreateOrderRawAsync(this HttpClient client, Guid? tableId = null)
    {
        return await client.PostAsJsonAsync("/api/orders", new { tableId });
    }

    public static async Task<HttpResponseMessage> AddItemAsync(this HttpClient client, Guid orderId, Guid productId, int quantity)
    {
        return await client.PostAsJsonAsync($"/api/orders/{orderId}/items", new { productId, quantity });
    }

    public static async Task<HttpResponseMessage> CheckoutAsync(this HttpClient client, Guid orderId, string paymentMethod = "Cash")
    {
        return await client.PostAsJsonAsync($"/api/orders/{orderId}/checkout", new { paymentMethod });
    }

    public static async Task<HttpResponseMessage> VoidOrderAsync(this HttpClient client, Guid orderId, string pin, string? reason = null)
    {
        return await client.PostAsJsonAsync($"/api/orders/{orderId}/void", new { pin, reason });
    }

    public static async Task<HttpResponseMessage> AdjustStockAsync(
        this HttpClient client, Guid ingredientId, decimal? countedQuantity = null, decimal? deltaQuantity = null,
        string? deltaReason = null, string reason = "test")
    {
        return await client.PostAsJsonAsync(
            $"/api/ingredients/{ingredientId}/stock-adjustment",
            new { countedQuantity, deltaQuantity, deltaReason, reason });
    }

    public static async Task<HttpResponseMessage> CreatePurchaseAsync(
        this HttpClient client, Guid supplierId, Guid ingredientId, decimal quantity, string unit, decimal unitCost)
    {
        return await client.PostAsJsonAsync("/api/purchases", new
        {
            supplierId,
            purchaseRequestId = (Guid?)null,
            items = new[] { new { ingredientId, quantity, unit, unitCost } }
        });
    }

    public static async Task<HttpResponseMessage> RecordPurchasePaymentAsync(this HttpClient client, Guid purchaseId, decimal amount)
    {
        return await client.PostAsJsonAsync($"/api/purchases/{purchaseId}/payments", new { amount });
    }

    private record LoginResponseDto(string Token);
    private record OrderDto(Guid Id);
}
