using Restaurant.Domain.Catalog;
using Restaurant.Domain.Payment;
using Restaurant.Domain.Sales;

namespace Restaurant.Api.Contracts;

public record TableResponse(Guid Id, string Number, TableStatus Status);

public record OpenShiftRequest(decimal OpeningCash);
public record ShiftResponse(Guid Id, DateTimeOffset OpenedAt, decimal OpeningCash, ShiftStatus Status);

public record CreateOrderRequest(Guid? TableId);

public record AddOrderItemRequest(Guid ProductId, int Quantity);

public record OrderItemResponse(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal, Station Station);

public record OrderResponse(
    Guid Id,
    Guid? TableId,
    Guid ShiftId,
    OrderStatus Status,
    decimal TotalAmount,
    List<OrderItemResponse> Items);

public record CheckoutRequest(PaymentMethodCode PaymentMethod);

public record CheckoutResponse(OrderResponse Order, Guid PaymentId, decimal AmountPaid, Guid JournalEntryId);
