using Restaurant.Domain.Common;

namespace Restaurant.Domain.Purchasing;

/// <summary>
/// The "Receiving" and "Invoice" halves of the PRD §11 Purchasing Flow, combined into
/// one record for this MVP extension: recording a Purchase increments Stock AND posts
/// the Accounts Payable journal line in the same step, instead of the PRD's formal
/// two-stage GoodsReceipt-then-Invoice split.
///
/// This combination is the one deliberate seam in this design (see the "jembatan"
/// discussion in project history): if a future need arises to split "goods physically
/// received" from "invoice recorded" (e.g. supplier delivers before sending the
/// invoice), that's an additive change — add a GoodsReceipt entity that Purchase
/// references, move the stock-increment there, leave the AP-posting on Purchase/
/// Invoice — not a rewrite of this entity or its data. Existing Purchase rows would
/// just mean "received and invoiced in the same step," which remains a valid state.
/// PurchaseRequestId is nullable because a Purchase can be recorded without ever
/// having gone through the PR/Approval flow (e.g. a walk-in emergency buy) — the
/// approval step is a control, not a hard prerequisite for stock/AP to move.
/// </summary>
public class Purchase : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid SupplierId { get; set; }
    public Guid? PurchaseRequestId { get; set; }
    public required Guid RecordedByUserId { get; set; }
    public decimal TotalAmount { get; set; }

    public List<PurchaseItem> Items { get; set; } = [];

    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.Subtotal);
    }
}

public class PurchaseItem : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid PurchaseId { get; set; }
    public required Guid IngredientId { get; set; }
    public required decimal Quantity { get; set; }
    public required string Unit { get; set; }
    public required decimal UnitCost { get; set; }
    public required decimal Subtotal { get; set; }
}
