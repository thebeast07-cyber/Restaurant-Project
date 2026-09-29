namespace Restaurant.Domain.Identity;

/// <summary>
/// Fixed role set for the Day-1 MVP. Custom RBAC/role builder is deferred
/// (see docs/architecture/01-mvp-technical-design.md, section 1.2).
/// </summary>
public enum UserRole
{
    Owner,
    Manager,
    Cashier
}
