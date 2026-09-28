namespace Restaurant.Api.Contracts;

public record VoidOrderRequest(string Pin, string? Reason);

public record VoidOrderResponse(OrderResponse Order, Guid? ReversalJournalEntryId);
