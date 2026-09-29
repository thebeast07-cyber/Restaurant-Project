using Restaurant.Domain.Common;

namespace Restaurant.Domain.Inventory;

/// <summary>
/// Quantity on hand for one Ingredient at one Branch. Invariant: Quantity must equal
/// the sum of all related StockMovement.ChangeQuantity, and must stay >= 0 for the
/// MVP (no negative-inventory override yet).
/// </summary>
public class Stock : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
}
