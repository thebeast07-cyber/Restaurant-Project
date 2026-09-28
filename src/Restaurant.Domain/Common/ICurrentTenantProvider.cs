namespace Restaurant.Domain.Common;

/// <summary>
/// Resolves the Tenant (and Branch) for the current execution context, sourced from
/// the authenticated user's JWT claims in a real request. Implemented in the Api layer.
///
/// TenantId == null is reserved for trusted system contexts (migrations, seeding) and
/// bypasses the Global Query Filter entirely — it must never be reachable from an
/// authenticated HTTP request.
/// </summary>
public interface ICurrentTenantProvider
{
    Guid? TenantId { get; }
    Guid? BranchId { get; }
}
