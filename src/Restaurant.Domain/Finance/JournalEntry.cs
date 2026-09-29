using Restaurant.Domain.Common;

namespace Restaurant.Domain.Finance;

/// <summary>
/// Double-entry journal posting. Immutable once created — corrections happen via a
/// reversal entry (IsReversal = true, ReversalOfId pointing back), never by editing
/// or deleting a posted entry (docs/product/PRD.md §12 "Posted journal tidak boleh
/// diubah secara destructive").
///
/// Invariant enforced in application code before every SaveChanges:
/// SUM(Lines.Debit) == SUM(Lines.Credit).
/// </summary>
public class JournalEntry : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string ReferenceType { get; set; }
    public required Guid ReferenceId { get; set; }
    public bool IsReversal { get; set; }
    public Guid? ReversalOfId { get; set; }

    public List<JournalLine> Lines { get; set; } = [];

    public void AssertBalanced()
    {
        var debit = Lines.Sum(l => l.Debit);
        var credit = Lines.Sum(l => l.Credit);
        if (debit != credit)
        {
            throw new InvalidOperationException(
                $"JournalEntry is not balanced: Debit={debit}, Credit={credit}. This must never happen — it indicates a bug in the posting logic, not bad input data.");
        }
    }
}

public class JournalLine : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid JournalEntryId { get; set; }
    public required Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
