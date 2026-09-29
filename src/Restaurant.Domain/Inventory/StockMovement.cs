using Restaurant.Domain.Common;

namespace Restaurant.Domain.Inventory;

public enum StockMovementReason
{
    Sale,
    Void,
    ManualAdjustment,
    Opname
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
}
