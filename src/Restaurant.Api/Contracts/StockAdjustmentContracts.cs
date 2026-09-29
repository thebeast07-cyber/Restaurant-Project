using Restaurant.Domain.Inventory;

namespace Restaurant.Api.Contracts;

/// <summary>
/// Exactly one of CountedQuantity (Opname — set stock to a physically counted
/// absolute value) or DeltaQuantity (a +/- correction) must be given. When
/// DeltaQuantity is given, DeltaReason picks which of the two delta-shaped reasons
/// this is — ManualAdjustment (e.g. a receiving/counting miscount) or Waste
/// (spoilage/breakage/expiry) — so the two can be reported on separately; it's
/// ignored (and not required) when CountedQuantity is used instead, since that case
/// is always Opname.
/// </summary>
public record StockAdjustmentRequest(
    decimal? CountedQuantity,
    decimal? DeltaQuantity,
    StockMovementReason? DeltaReason,
    string Reason);

public record StockAdjustmentResponse(
    Guid IngredientId,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal ChangeQuantity,
    string MovementReason);
