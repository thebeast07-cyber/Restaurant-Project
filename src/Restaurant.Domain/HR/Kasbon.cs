using Restaurant.Domain.Common;

namespace Restaurant.Domain.HR;

public enum KasbonStatus
{
    Pending,
    Approved,
    Rejected,
    Settled
}

/// <summary>
/// Salary advance. Requested, then Owner/Manager approves or rejects
/// (<see cref="ApprovedByUserId"/>/<see cref="ApprovedAt"/> set on Approve — this also
/// doubles as the disbursement moment: a small restaurant hands over the cash directly,
/// off the formal Finance ledger, same as the request/approval itself isn't booked —
/// see PayrollController for how repayment nets out instead).
///
/// Repayment defaults to lump-sum (<see cref="InstallmentCount"/> = 1) per the owner's
/// answer during HR design (2026-10-01), but deliberately modeled as a count rather than
/// a hardcoded single payment: this wasn't signed off by management yet, and if they
/// later want multi-month installments instead, that's a config value
/// (InstallmentCount > 1), not a schema rework. Each Payroll run deducts
/// min(Amount / InstallmentCount, remaining balance, that period's available GrossPay)
/// from <see cref="AmountRepaid"/> until it reaches <see cref="Amount"/>, at which point
/// Status becomes Settled.
/// </summary>
public class Kasbon : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid EmployeeId { get; set; }
    public required decimal Amount { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public KasbonStatus Status { get; set; } = KasbonStatus.Pending;
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public decimal AmountRepaid { get; set; }
    public string? Notes { get; set; }
}
