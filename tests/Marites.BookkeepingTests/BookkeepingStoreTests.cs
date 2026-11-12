using Marites.Domain.Tenancy;
using Marites.Infrastructure.Bookkeeping;
using Microsoft.EntityFrameworkCore;

namespace Marites.BookkeepingTests;

/// <summary>
/// The store's two promises: migrations create the schema, and a value that goes in comes out
/// unchanged. The second is not free — DEC-020 overrides the provider's default
/// <c>DateTimeOffset</c> format, so the exact stored text is asserted rather than assumed.
/// </summary>
public sealed class BookkeepingStoreTests
{
    [Fact]
    public void Migrations_create_the_bookkeeping_schema()
    {
        using var store = new TemporaryStore();
        using var context = store.CreateContext();

        context.Database.Migrate();

        var tables = context.Database
            .SqlQueryRaw<string>("select name as \"Value\" from sqlite_master where type = 'table'")
            .ToList();

        Assert.Contains("tenant", tables);
        Assert.Contains("app_user", tables);
        Assert.Contains("broker_profile", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public void Tenant_round_trips_and_is_stored_in_the_documented_format()
    {
        using var store = new TemporaryStore();
        using var context = store.CreateContext();
        context.Database.Migrate();

        var now = new DateTimeOffset(2026, 11, 12, 8, 30, 15, TimeSpan.Zero).AddTicks(1234567);
        var tenant = Tenant.Create("Acme", "Acme", now);

        context.Tenants.Add(tenant);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var loaded = context.Tenants.Single();

        Assert.Equal(tenant.Id, loaded.Id);
        Assert.Equal("Acme", loaded.Name);
        Assert.Equal("acme", loaded.Slug);
        Assert.Equal(now, loaded.CreatedAt);

        // The mapping DEC-020 fixes, read back as text rather than through the converter.
        var rawId = context.Database
            .SqlQueryRaw<string>("select id as \"Value\" from tenant")
            .Single();
        var rawCreatedAt = context.Database
            .SqlQueryRaw<string>("select created_at as \"Value\" from tenant")
            .Single();

        Assert.Equal(tenant.Id.ToString("D"), rawId);
        Assert.Equal("2026-11-12T08:30:15.1234567Z", rawCreatedAt);
        Assert.Equal(36, rawId.Length);
    }

    private sealed class TemporaryStore : IDisposable
    {
        private readonly string _path =
            Path.Combine(Path.GetTempPath(), $"marites-bookkeeping-{Guid.NewGuid():N}.db");

        public BookkeepingDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BookkeepingDbContext>()
                .UseSqlite($"Data Source={_path}")
                .Options;

            return new BookkeepingDbContext(options);
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
