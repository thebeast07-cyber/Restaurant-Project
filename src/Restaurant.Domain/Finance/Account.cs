using Restaurant.Domain.Common;

namespace Restaurant.Domain.Finance;

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

/// <summary>
/// Chart of Accounts entry. MVP seeds a minimal set (Cash, Revenue, InventoryAsset,
/// COGS) — full chart-of-accounts management UI is out of scope
/// (docs/product/PRD.md §16).
/// </summary>
public class Account : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required AccountType Type { get; set; }
}
