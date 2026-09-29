using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Tests;

/// <summary>
/// Runs the real Api against the dev Postgres (docker-compose) — there's no separate
/// test database yet (see implementation-notes.md). Fine for now since this is a
/// single-dev sprint, but a real risk if these tests run against a shared/CI database
/// later: flagged as a fast-follow, not silently assumed away.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
