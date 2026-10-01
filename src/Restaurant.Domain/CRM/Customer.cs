using Restaurant.Domain.Common;

namespace Restaurant.Domain.CRM;

/// <summary>
/// First slice of the deferred CRM & Promotion phase (see docs/architecture/
/// 02-ui-roadmap.md item 10) — pulled forward specifically for Self-Order +
/// Digital Receipt, not a throwaway table. The full CRM phase later builds
/// loyalty/segmentation on top of this same entity.
///
/// Phone is the dedupe key: a repeat customer accumulates under one record
/// across visits (upserted in SelfOrderController) instead of a fresh row every
/// time, which is what makes this usable for marketing later rather than just a
/// one-off contact capture.
/// </summary>
public class Customer : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string Name { get; set; }
    public required string Phone { get; set; }
}
