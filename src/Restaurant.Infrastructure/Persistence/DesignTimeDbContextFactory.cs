using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` construct AppDbContext without a running DI container.
/// Connection string here is only used to generate migration files (schema), never to
/// actually connect during `dotnet ef migrations add` — safe to keep a local dev default.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RESTAURANT_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=restaurant_db;Username=restaurant;Password=restaurant_dev_only";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options, new SystemTenantProvider());
    }
}
