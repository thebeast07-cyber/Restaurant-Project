using Restaurant.Domain.Common;

namespace Restaurant.Domain.Purchasing;

/// <summary>
/// A vendor goods are purchased from. Kept minimal for this MVP extension — no
/// payment terms, tax ID, or multi-contact support yet, just enough to attribute a
/// Purchase and its resulting Accounts Payable to someone.
/// </summary>
public class Supplier : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string Name { get; set; }
    public string? ContactInfo { get; set; }
    public bool IsActive { get; set; } = true;
}
