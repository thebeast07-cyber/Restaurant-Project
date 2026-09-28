using Restaurant.Domain.Common;

namespace Restaurant.Domain.Sales;

public enum ShiftStatus
{
    Open,
    Closed
}

/// <summary>
/// Minimal for Day 3: only what's needed to attach an Order to a cashier's session.
/// Full cash reconciliation (ClosingCash, close flow) lands Day 11.
/// </summary>
public class Shift : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid UserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
}
