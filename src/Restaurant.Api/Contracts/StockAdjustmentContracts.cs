namespace Restaurant.Api.Contracts;

/// <summary>
/// Exactly one of CountedQuantity (Opname — set stock to a physically counted
/// absolute value) or DeltaQuantity (ManualAdjustment — apply a known +/- correction,
/// e.g. spoilage or a receiving miscount) must be given; the movement Reason is
/// derived from which one was sent, not passed explicitly.
/// </summary>
public record StockAdjustmentRequest(decimal? CountedQuantity, decimal? DeltaQuantity, string Reason);

public record StockAdjustmentResponse(
    Guid IngredientId,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal ChangeQuantity,
    string MovementReason);
