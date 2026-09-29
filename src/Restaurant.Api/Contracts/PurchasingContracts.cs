using Restaurant.Domain.Purchasing;

namespace Restaurant.Api.Contracts;

public record SupplierResponse(Guid Id, string Name, string? ContactInfo, bool IsActive);
public record CreateSupplierRequest(string Name, string? ContactInfo);

public record PurchaseRequestItemRequest(Guid IngredientId, decimal Quantity, string Unit);
public record PurchaseRequestItemResponse(Guid IngredientId, string IngredientName, decimal Quantity, string Unit);

public record CreatePurchaseRequestRequest(RequestedFor RequestedFor, string? Notes, List<PurchaseRequestItemRequest> Items);

public record PurchaseRequestResponse(
    Guid Id,
    RequestedFor RequestedFor,
    string? Notes,
    PurchaseRequestStatus Status,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? ReviewNotes,
    List<PurchaseRequestItemResponse> Items);

public record ReviewPurchaseRequestRequest(string? Notes);

public record PurchaseItemRequest(Guid IngredientId, decimal Quantity, string Unit, decimal UnitCost);
public record PurchaseItemResponse(Guid IngredientId, string IngredientName, decimal Quantity, string Unit, decimal UnitCost, decimal Subtotal);

public record CreatePurchaseRequest(Guid SupplierId, Guid? PurchaseRequestId, List<PurchaseItemRequest> Items);

public record PurchaseResponse(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid? PurchaseRequestId,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal RemainingBalance,
    PurchasePaymentStatus PaymentStatus,
    Guid JournalEntryId,
    List<PurchaseItemResponse> Items);

public record RecordPurchasePaymentRequest(decimal Amount);

public record PurchasePaymentResponse(
    Guid PurchaseId,
    decimal AmountPaid,
    decimal RemainingBalance,
    PurchasePaymentStatus PaymentStatus,
    Guid JournalEntryId);
