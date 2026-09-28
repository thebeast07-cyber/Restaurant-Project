using Restaurant.Domain.Common;

namespace Restaurant.Api.Auth;

/// <summary>
/// Resolves TenantId/BranchId from the authenticated request's JWT claims.
/// Returns null (bypasses the tenant filter) when there is no authenticated user —
/// this only happens for anonymous endpoints like /api/auth/login, which never touch
/// tenant-scoped tables directly (they query Users by username across the seeded data
/// at startup time only, see AuthController).
/// </summary>
public class HttpCurrentTenantProvider : ICurrentTenantProvider
{
    public Guid? TenantId { get; }
    public Guid? BranchId { get; }

    public HttpCurrentTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var tenantClaim = user.FindFirst(AppClaimTypes.TenantId)?.Value;
        var branchClaim = user.FindFirst(AppClaimTypes.BranchId)?.Value;

        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            TenantId = tenantId;
        }

        if (Guid.TryParse(branchClaim, out var branchId))
        {
            BranchId = branchId;
        }
    }
}
