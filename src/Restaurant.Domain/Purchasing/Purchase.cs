using Restaurant.Domain.Common;

namespace Restaurant.Domain.Purchasing;

public enum PurchasePaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid
}

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

    /// <summary>
    /// Running total of PurchasePayment.Amount for this Purchase. Kept denormalized
    /// (not just summed from PurchasePayments on read) so the "would this payment
    /// overpay?" check in PurchasesController.RecordPayment can be a single atomic
    /// conditional UPDATE — same discipline as every other shared-counter mutation in
    /// this codebase.
    /// </summary>
    public decimal AmountPaid { get; set; }
    public PurchasePaymentStatus PaymentStatus { get; set; } = PurchasePaymentStatus.Unpaid;

    public List<PurchaseItem> Items { get; set; } = [];

    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.Subtotal);
    }
}

/// <summary>
/// One installment against a Purchase's Accounts Payable balance. Supports partial
/// payment (cicilan) by design — a Purchase can take several of these before
/// PaymentStatus reaches Paid. Append-only, like AuditLog: a correction is a new
/// (possibly negative-amount reversal — not yet needed/built) row, not an edit.
/// </summary>
public class PurchasePayment : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid PurchaseId { get; set; }
    public required decimal Amount { get; set; }
    public required Guid PaidByUserId { get; set; }
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
