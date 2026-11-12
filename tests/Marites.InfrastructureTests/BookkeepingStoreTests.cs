using Marites.Domain.Tenancy;
using Marites.Infrastructure.Bookkeeping;
using Microsoft.EntityFrameworkCore;

namespace Marites.InfrastructureTests;

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

    [Fact]
    public void The_audit_table_refuses_update_and_delete()
    {
        using var store = new TemporaryStore();
        using var context = store.CreateContext();
        context.Database.Migrate();

        context.Database.ExecuteSqlRaw(
            "insert into operation_audit (id, tenant_id, actor_kind, action, target, params_json, result, payload_edited, created_at) " +
            "values ('00000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002','human','peek','orders',{0},'ok',0,'2026-11-12T00:00:00.0000000Z')",
            // Passed as a parameter because ExecuteSqlRaw treats braces in the SQL as format items.
            "{}");

        // Append-only is a property of the schema, not of the repository that happens to use it.
        var update = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(
            () => context.Database.ExecuteSqlRaw("update operation_audit set result = 'tampered'"));
        Assert.Contains("append-only", update.Message);

        var delete = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(
            () => context.Database.ExecuteSqlRaw("delete from operation_audit"));
        Assert.Contains("append-only", delete.Message);
    }

}
