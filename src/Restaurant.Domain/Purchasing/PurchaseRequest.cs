using Restaurant.Domain.Common;

namespace Restaurant.Domain.Purchasing;

public enum PurchaseRequestStatus
{
    Pending,
    Approved,
    Rejected,

    /// <summary>
    /// A Purchase has been recorded against this request (see PurchasesController.Create).
    /// Terminal, like Rejected — once Fulfilled, no further Purchase can reference this
    /// request (guarded by the same atomic claim pattern used everywhere else in this
    /// codebase for a status a row can only leave once). Known simplification: this
    /// models "the whole request was fulfilled by one Purchase," not partial
    /// fulfillment (e.g. requesting 50kg but only 30kg actually purchased so far) —
    /// there's no per-item requested-vs-fulfilled-quantity tracking.
    /// </summary>
    Fulfilled
}

/// <summary>
/// Who the request is on behalf of. Deliberately NOT tied to a real user account —
/// Kitchen/Bar staff don't have logins in this system (only Owner/Manager/Cashier
/// exist, see Identity notes), so a Manager keys in the request for them. This is
/// just a label for traceability, not an authorization boundary.
/// </summary>
public enum RequestedFor
{
    Kitchen,
    Bar,
    General
}

/// <summary>
/// The "Approval" half of the PRD §11 Purchasing Flow (Purchase Request → Approval →
/// PO → Receiving → Inventory & AP). Manager creates it (on behalf of Kitchen/Bar,
/// since those don't have their own accounts), Owner approves or rejects.
///
/// Deliberately does NOT have its own PO/Sent/PartiallyReceived lifecycle — approval
/// here leads straight to recording a Purchase (see Purchase.cs), combining what the
/// PRD's formal flow treats as separate PO + Receiving + Invoice stages. This is the
/// same class of simplification as Day 6 (QRIS-manual instead of a real gateway) and
/// Day 10 (Owner/Manager standing in for "Warehouse Staff", which doesn't exist as a
/// role here) — not a new kind of scope-cutting for this codebase.
/// </summary>
public class PurchaseRequest : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid RequestedByUserId { get; set; }
    public required RequestedFor RequestedFor { get; set; }
    public string? Notes { get; set; }
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Pending;

    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public List<PurchaseRequestItem> Items { get; set; } = [];
}

public class PurchaseRequestItem : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid PurchaseRequestId { get; set; }
    public required Guid IngredientId { get; set; }
    public required decimal Quantity { get; set; }
    public required string Unit { get; set; }
}
