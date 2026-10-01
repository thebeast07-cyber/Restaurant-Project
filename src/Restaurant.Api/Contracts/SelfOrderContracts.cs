using Restaurant.Domain.Sales;

namespace Restaurant.Api.Contracts;

public record SelfOrderMenuVariant(Guid Id, string Name, decimal Price);

public record SelfOrderMenuItem(Guid ProductId, string Name, string CategoryName, decimal Price, string? ImageUrl, List<SelfOrderMenuVariant> Variants);

public record SelfOrderTableStateResponse(
    string TableNumber, bool IsOpen, OrderResponse? ActiveOrder, List<SelfOrderMenuItem> Menu);

public record StartSelfOrderRequest(string Name, string Phone);

public record SelfOrderReceiptItem(string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);

public record SelfOrderReceiptResponse(
    Guid OrderId, string? TableNumber, DateTimeOffset CompletedAt, List<SelfOrderReceiptItem> Items, decimal TotalAmount);
