using Restaurant.Domain.Common;

namespace Restaurant.Domain.Identity;

public class User : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string Name { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Hash of the Manager PIN used to authorize sensitive POS actions (void, stock adjustment).
    /// Null for roles that never authorize such actions (e.g. Cashier).
    /// </summary>
    public string? PinHash { get; set; }

    public required UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
