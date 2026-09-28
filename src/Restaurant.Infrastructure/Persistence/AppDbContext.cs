using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Common;
using Restaurant.Domain.Identity;
using Restaurant.Domain.Organization;

namespace Restaurant.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>().ToTable("tenants");

        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("branches");
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<User>(u =>
        {
            u.ToTable("users");
            u.HasIndex(x => x.TenantId);
            u.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();
        });

        modelBuilder.Entity<Category>(c =>
        {
            c.ToTable("categories");
            c.HasIndex(x => new { x.TenantId, x.BranchId });
        });

        modelBuilder.Entity<Ingredient>(i =>
        {
            i.ToTable("ingredients");
            i.HasIndex(x => new { x.TenantId, x.BranchId });
        });

        modelBuilder.Entity<Product>(p =>
        {
            p.ToTable("products");
            p.HasIndex(x => new { x.TenantId, x.BranchId });
            p.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecipeItem>(r =>
        {
            r.ToTable("recipe_items");
            r.HasIndex(x => x.ProductId);
            r.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne<Product>().WithMany(p => p.RecipeItems).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Enforces row-level Tenant isolation (docs/discovery/11-multitenancy.md, Option A):
    /// every entity implementing ITenantScoped automatically gets
    /// `WHERE TenantId = @currentTenantId` appended at the ORM level, so a forgotten
    /// manual filter in application code can never leak data across tenants.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(BuildTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(clrType);

            var filter = method.Invoke(this, null);
            modelBuilder.Entity(clrType).HasQueryFilter((LambdaExpression)filter!);
        }
    }

    private LambdaExpression BuildTenantFilter<TEntity>() where TEntity : class, ITenantScoped
    {
        Expression<Func<TEntity, bool>> filter = e =>
            _tenantProvider.TenantId == null || e.TenantId == _tenantProvider.TenantId;
        return filter;
    }
}
