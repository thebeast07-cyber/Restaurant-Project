namespace Restaurant.Api.Contracts;

public record StockOpnameLineRequest(Guid IngredientId, decimal CountedQuantity);

public record CreateStockOpnameSessionRequest(DateOnly Date, string? Notes, List<StockOpnameLineRequest> Lines);

public record StockOpnameLineResponse(
    Guid IngredientId, string IngredientName, string Unit, decimal QuantityBefore, decimal QuantityAfter, decimal ChangeQuantity);

public record StockOpnameSessionResponse(
    Guid Id, DateOnly Date, string? Notes, Guid CreatedByUserId, List<StockOpnameLineResponse> Lines);
