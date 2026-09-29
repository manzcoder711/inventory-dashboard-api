using System.Security.Cryptography;
using InventoryApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryApi.Tests;

/// <summary>
/// Hosts the real API in memory, with Azure SQL swapped for SQLite and no real secrets loaded.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();

        // The schema must exist before the app starts, because Program.cs seeds the demo
        // account and starter products during startup.
        using var context = new InventoryDbContext(
            new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        context.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that environment loads user-secrets, i.e. the real Azure SQL
        // connection string and JWT signing key. This keeps the test hermetic and CI-safe.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        // ConfigureTestServices runs after Program.cs registers its own services, so the
        // removals below find something to remove.
        builder.ConfigureTestServices(services =>
        {
            // EF Core 9+ stores the UseSqlServer(...) call in IDbContextOptionsConfiguration<T>,
            // separately from DbContextOptions<T>; both must go or two providers get registered.
            services.RemoveAll<DbContextOptions<InventoryDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<InventoryDbContext>>();
            services.AddDbContext<InventoryDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
