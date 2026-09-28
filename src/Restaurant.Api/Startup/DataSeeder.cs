using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Identity;
using Restaurant.Domain.Organization;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Startup;

/// <summary>
/// Seeds the single Tenant/Branch and the 3 fixed-role users for the Day-1 MVP
/// (docs/architecture/01-mvp-technical-design.md, section 1.1-1.2).
/// Idempotent: no-ops if a Tenant already exists.
/// Credentials here are local dev-only test accounts, not real secrets.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Tenants.AnyAsync())
        {
            return;
        }

        var tenant = new Tenant { Name = "Restaurant Demo Tenant" };
        db.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Main Branch",
            Address = "TBD"
        };
        db.Branches.Add(branch);

        db.Users.AddRange(
            new User
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "Owner",
                Username = "owner",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner#12345"),
                PinHash = BCrypt.Net.BCrypt.HashPassword("1111"),
                Role = UserRole.Owner
            },
            new User
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "Manager",
                Username = "manager",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager#12345"),
                PinHash = BCrypt.Net.BCrypt.HashPassword("2222"),
                Role = UserRole.Manager
            },
            new User
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "Cashier",
                Username = "cashier",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Cashier#12345"),
                Role = UserRole.Cashier
            });

        await db.SaveChangesAsync();
    }
}
