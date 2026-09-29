namespace Restaurant.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single Tenant. Any entity implementing this
/// is automatically scoped by TenantId via EF Core Global Query Filter (see AppDbContext).
/// This is the row-level isolation mechanism agreed in docs/discovery/11-multitenancy.md.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}

/// <summary>
/// Marks an entity as belonging to a single Branch within a Tenant.
/// </summary>
public interface IBranchScoped : ITenantScoped
{
    Guid BranchId { get; }
}
