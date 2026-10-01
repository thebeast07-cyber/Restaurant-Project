using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Audit;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.CRM;
using Restaurant.Domain.Common;
using Restaurant.Domain.Finance;
using Restaurant.Domain.HR;
using Restaurant.Domain.Identity;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Organization;
using Restaurant.Domain.Payment;
using Restaurant.Domain.Purchasing;
using Restaurant.Domain.Sales;

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
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockOpnameSession> StockOpnameSessions => Set<StockOpnameSession>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Domain.Payment.Payment> Payments => Set<Domain.Payment.Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestItem> PurchaseRequestItems => Set<PurchaseRequestItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<OperatingExpense> OperatingExpenses => Set<OperatingExpense>();
    public DbSet<OperatingExpensePayment> OperatingExpensePayments => Set<OperatingExpensePayment>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Kasbon> Kasbons => Set<Kasbon>();
    public DbSet<Payslip> Payslips => Set<Payslip>();

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
            r.HasIndex(x => x.ProductVariantId);
            r.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne<Product>().WithMany(p => p.RecipeItems).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            r.HasOne<ProductVariant>().WithMany(v => v.RecipeItems).HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductVariant>(v =>
        {
            v.ToTable("product_variants");
            v.HasIndex(x => x.ProductId);
            v.HasOne<Product>().WithMany(p => p.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RestaurantTable>(t =>
        {
            t.ToTable("tables");
            t.HasIndex(x => new { x.TenantId, x.BranchId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Shift>(s =>
        {
            s.ToTable("shifts");
            s.HasIndex(x => new { x.TenantId, x.BranchId, x.UserId, x.Status });

            // Enforces "at most one open shift per user" atomically at the DB level —
            // the application-level check-then-insert in ShiftsController.Open is
            // TOCTOU-vulnerable (two near-simultaneous requests could both pass the
            // check). This partial unique index makes the race impossible instead of
            // just unlikely. Status = 0 is ShiftStatus.Open.
            s.HasIndex(x => x.UserId).IsUnique().HasFilter("\"Status\" = 0");
        });

        modelBuilder.Entity<Order>(o =>
        {
            o.ToTable("orders");
            o.HasIndex(x => new { x.TenantId, x.BranchId, x.Status });
            o.HasOne<RestaurantTable>().WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
            o.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
            o.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(c =>
        {
            c.ToTable("customers");
            c.HasIndex(x => new { x.TenantId, x.BranchId, x.Phone }).IsUnique();
        });

        modelBuilder.Entity<OrderItem>(oi =>
        {
            oi.ToTable("order_items");
            oi.HasIndex(x => x.OrderId);
            oi.HasOne<Order>().WithMany(o => o.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            oi.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            oi.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Stock>(s =>
        {
            s.ToTable("stocks");
            s.HasIndex(x => new { x.TenantId, x.BranchId, x.IngredientId }).IsUnique();
            s.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(sm =>
        {
            sm.ToTable("stock_movements");
            sm.HasIndex(x => new { x.TenantId, x.BranchId, x.IngredientId });
            sm.HasIndex(x => x.OpnameSessionId);
            sm.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            sm.HasOne<StockOpnameSession>().WithMany().HasForeignKey(x => x.OpnameSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockOpnameSession>(s =>
        {
            s.ToTable("stock_opname_sessions");
            s.HasIndex(x => new { x.TenantId, x.BranchId });
        });

        modelBuilder.Entity<Account>(a =>
        {
            a.ToTable("accounts");
            a.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<JournalEntry>(je =>
        {
            je.ToTable("journal_entries");
            je.HasIndex(x => new { x.TenantId, x.BranchId, x.ReferenceType, x.ReferenceId });
            je.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JournalLine>(jl =>
        {
            jl.ToTable("journal_lines");
            jl.HasOne<JournalEntry>().WithMany(je => je.Lines).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            jl.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentMethod>(pm =>
        {
            pm.ToTable("payment_methods");
            pm.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<Domain.Payment.Payment>(p =>
        {
            p.ToTable("payments");
            p.HasIndex(x => x.OrderId);
            p.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            p.HasOne<PaymentMethod>().WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(a =>
        {
            a.ToTable("audit_logs");
            a.HasIndex(x => new { x.TenantId, x.BranchId, x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<Supplier>(s =>
        {
            s.ToTable("suppliers");
            s.HasIndex(x => new { x.TenantId, x.BranchId });
        });

        modelBuilder.Entity<PurchaseRequest>(pr =>
        {
            pr.ToTable("purchase_requests");
            pr.HasIndex(x => new { x.TenantId, x.BranchId, x.Status });
        });

        modelBuilder.Entity<PurchaseRequestItem>(pri =>
        {
            pri.ToTable("purchase_request_items");
            pri.HasIndex(x => x.PurchaseRequestId);
            pri.HasOne<PurchaseRequest>().WithMany(pr => pr.Items).HasForeignKey(x => x.PurchaseRequestId).OnDelete(DeleteBehavior.Cascade);
            pri.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Purchase>(p =>
        {
            p.ToTable("purchases");
            p.HasIndex(x => new { x.TenantId, x.BranchId });
            p.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            p.HasOne<PurchaseRequest>().WithMany().HasForeignKey(x => x.PurchaseRequestId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseItem>(pi =>
        {
            pi.ToTable("purchase_items");
            pi.HasIndex(x => x.PurchaseId);
            pi.HasOne<Purchase>().WithMany(p => p.Items).HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            pi.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchasePayment>(pp =>
        {
            pp.ToTable("purchase_payments");
            pp.HasIndex(x => x.PurchaseId);
            pp.HasOne<Purchase>().WithMany().HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OperatingExpense>(oe =>
        {
            oe.ToTable("operating_expenses");
            oe.HasIndex(x => new { x.TenantId, x.BranchId });
        });

        modelBuilder.Entity<OperatingExpensePayment>(oep =>
        {
            oep.ToTable("operating_expense_payments");
            oep.HasIndex(x => x.OperatingExpenseId);
            oep.HasOne<OperatingExpense>().WithMany().HasForeignKey(x => x.OperatingExpenseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.ToTable("employees");
            e.HasIndex(x => new { x.TenantId, x.BranchId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Attendance>(a =>
        {
            a.ToTable("attendances");
            a.HasIndex(x => new { x.TenantId, x.BranchId, x.EmployeeId, x.Date }).IsUnique();
            a.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Kasbon>(k =>
        {
            k.ToTable("kasbons");
            k.HasIndex(x => new { x.TenantId, x.BranchId, x.EmployeeId, x.Status });
            k.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payslip>(p =>
        {
            p.ToTable("payslips");
            p.HasIndex(x => new { x.TenantId, x.BranchId, x.EmployeeId, x.PeriodYear, x.PeriodMonth }).IsUnique();
            p.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            p.HasOne<OperatingExpense>().WithMany().HasForeignKey(x => x.OperatingExpenseId).OnDelete(DeleteBehavior.Restrict);
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
