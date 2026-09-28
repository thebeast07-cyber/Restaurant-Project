using Restaurant.Domain.Common;

namespace Restaurant.Infrastructure.Persistence;

/// <summary>
/// Trusted system-context tenant provider (TenantId/BranchId = null bypasses the
/// Global Query Filter). Used only for design-time migrations and startup seeding —
/// never registered for an authenticated HTTP request.
/// </summary>
public class SystemTenantProvider : ICurrentTenantProvider
{
    public Guid? TenantId => null;
    public Guid? BranchId => null;
}
