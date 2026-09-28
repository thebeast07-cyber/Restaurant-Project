using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
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

        // Sample catalog data (placeholder — replace with the real menu before go-live,
        // see docs/architecture/01-mvp-technical-design.md section 5).
        var makanan = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = "Makanan" };
        var minuman = new Category { TenantId = tenant.Id, BranchId = branch.Id, Name = "Minuman" };
        db.Categories.AddRange(makanan, minuman);

        var beras = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = "Beras", Unit = "gram" };
        var telur = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = "Telur", Unit = "pcs" };
        var ayam = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = "Ayam", Unit = "gram" };
        var tehCelup = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = "Teh Celup", Unit = "pcs" };
        var esBatu = new Ingredient { TenantId = tenant.Id, BranchId = branch.Id, Name = "Es Batu", Unit = "gram" };
        db.Ingredients.AddRange(beras, telur, ayam, tehCelup, esBatu);

        var nasiGoreng = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = makanan.Id,
            Name = "Nasi Goreng Ayam",
            Price = 25000m,
            Station = Station.Kitchen
        };
        nasiGoreng.RecipeItems.AddRange(
        [
            new RecipeItem { TenantId = tenant.Id, ProductId = nasiGoreng.Id, IngredientId = beras.Id, Quantity = 200, Unit = "gram" },
            new RecipeItem { TenantId = tenant.Id, ProductId = nasiGoreng.Id, IngredientId = telur.Id, Quantity = 1, Unit = "pcs" },
            new RecipeItem { TenantId = tenant.Id, ProductId = nasiGoreng.Id, IngredientId = ayam.Id, Quantity = 80, Unit = "gram" }
        ]);

        var esTeh = new Product
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            CategoryId = minuman.Id,
            Name = "Es Teh Manis",
            Price = 8000m,
            Station = Station.Bar
        };
        esTeh.RecipeItems.AddRange(
        [
            new RecipeItem { TenantId = tenant.Id, ProductId = esTeh.Id, IngredientId = tehCelup.Id, Quantity = 1, Unit = "pcs" },
            new RecipeItem { TenantId = tenant.Id, ProductId = esTeh.Id, IngredientId = esBatu.Id, Quantity = 100, Unit = "gram" }
        ]);

        db.Products.AddRange(nasiGoreng, esTeh);

        await db.SaveChangesAsync();
    }
}
