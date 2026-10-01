using Restaurant.Domain.Common;

namespace Restaurant.Domain.Inventory;

public enum StockMovementReason
{
    Sale,
    Void,
    ManualAdjustment,
    Opname,

    /// <summary>Stock increment from a recorded Purchase (see Restaurant.Domain.Purchasing.Purchase).</summary>
    Purchase,

    /// <summary>
    /// Spoilage/breakage/expiry — split out from ManualAdjustment (not in the
    /// original PRD; added by explicit agreement) specifically so it can be reported
    /// on separately as a real business cost, instead of being indistinguishable from
    /// an ordinary counting correction. Recorded through the same
    /// IngredientsController.AdjustStock endpoint as ManualAdjustment/Opname, just a
    /// different reason value — no new endpoint needed.
    /// </summary>
    Waste
}

/// <summary>
/// Audit trail of why Stock changed. Every Stock.Quantity change must have a
/// corresponding StockMovement row — this is what "Real-Time Stock Deduction" and
/// audit requirements (docs/product/PRD.md §15, §20) actually mean in code.
/// </summary>
public class StockMovement : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid IngredientId { get; set; }
    public required decimal ChangeQuantity { get; set; }
    public required StockMovementReason Reason { get; set; }
    public required string ReferenceType { get; set; }
    public required Guid ReferenceId { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Set only for an Opname recorded as part of a batched
    /// <see cref="StockOpnameSession"/> (StockOpnameController) — null for a
    /// single-ingredient Opname via IngredientsController.AdjustStock, and for every
    /// other Reason.</summary>
    public Guid? OpnameSessionId { get; set; }

    /// <summary>
    /// Snapshot of Ingredient.AverageCost at the moment this movement was recorded —
    /// null for movements where a cost snapshot isn't meaningful (Sale/Void post COGS
    /// straight to the JournalEntry instead; Purchase's cost is already on
    /// PurchaseItem.UnitCost). Currently only populated for Waste (and
    /// ManualAdjustment, for consistency) by IngredientsController.AdjustStock — this
    /// is what GET /api/reports/waste multiplies against ChangeQuantity to report a
    /// real Rupiah figure instead of just a quantity.
    /// </summary>
    public decimal? UnitCostAtTime { get; set; }

    /// <summary>
    /// Quantity immediately before this movement applied — populated only for Opname
    /// (both the single-ingredient endpoint and a batched StockOpnameSession), same
    /// "nullable, reason-specific" pattern as UnitCostAtTime above. Every other Reason
    /// is a pure delta (Sale/Purchase/etc. don't need a "before" to be meaningful),
    /// but Opname's whole point is showing what was counted against what the system
    /// said, so the before value is worth keeping rather than re-derived later from
    /// current Stock.Quantity (which may have moved on from other movements since).
    /// </summary>
    public decimal? QuantityBefore { get; set; }
}
