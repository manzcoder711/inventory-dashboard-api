using InventoryApi.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Tests;

// SQLite in-memory rather than EF Core's InMemory provider: SQLite is a real relational
// database, so the unique indexes on Sku and Email are actually enforced.
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        // An in-memory SQLite database is deleted as soon as its connection closes,
        // so the connection stays open for the lifetime of the test.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // EnsureCreated builds the schema from the EF model; the migrations are SQL Server-specific.
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    // Tests assert through a fresh context, so they see what was really saved rather than
    // what the service's context happens to be tracking in memory.
    public InventoryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
