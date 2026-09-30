using Restaurant.Domain.Common;

namespace Restaurant.Domain.Finance;

public enum ExpenseCategory
{
    Sewa,
    Gaji,
    Utilitas,
    Marketing,
    Lainnya
}

public enum ExpensePaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid
}

/// <summary>
/// Non-COGS operating cost (rent, salary, utilities, marketing, etc.) — the piece
/// that was missing for a P&L to report a real Net Profit instead of stopping at
/// Gross Profit. Recorded on an accrual basis, same Accounts-Payable pattern as
/// <see cref="Restaurant.Domain.Purchasing.Purchase"/>: recording an expense posts
/// Debit OperatingExpense(6000) / Credit AccountsPayable(2000) at IncurredAt (when
/// the cost was economically incurred), not an immediate-cash assumption — an
/// expense billed this month but paid next month must still count in this month's
/// P&L. A separate <see cref="OperatingExpensePayment"/> posts the AP -> Cash
/// settlement later, exactly mirroring PurchasePayment.
/// </summary>
public class OperatingExpense : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required ExpenseCategory Category { get; set; }
    public required string Description { get; set; }
    public required decimal Amount { get; set; }
    public required DateOnly IncurredAt { get; set; }
    public required Guid RecordedByUserId { get; set; }

    /// <summary>Denormalized running total, same reasoning as Purchase.AmountPaid — lets
    /// the "would this overpay?" check in the payment endpoint be a single atomic
    /// conditional UPDATE instead of a separate read-then-write.</summary>
    public decimal AmountPaid { get; set; }
    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Unpaid;
}

/// <summary>One installment against an OperatingExpense's Accounts Payable balance —
/// mirrors PurchasePayment exactly (append-only, supports partial payment).</summary>
public class OperatingExpensePayment : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid OperatingExpenseId { get; set; }
    public required decimal Amount { get; set; }
    public required Guid PaidByUserId { get; set; }
}
