using Marites.Infrastructure.Bookkeeping;
using Microsoft.EntityFrameworkCore;

namespace Marites.InfrastructureTests;

/// <summary>
/// The bootstrap's two promises: a fresh store ends up usable, and running the step twice does
/// not produce a second tenant — a deploy that reruns migrations is normal, not exceptional.
/// </summary>
public sealed class BookkeepingMigratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrating_a_fresh_store_seeds_one_tenant()
    {
        using var store = new TemporaryStore();
        await using var context = store.CreateContext();

        var seeded = await new BookkeepingMigrator(context).MigrateAndSeedAsync("Default", "default", Now);

        Assert.True(seeded);

        await using var check = store.CreateContext();
        var tenant = await check.Tenants.SingleAsync();
        Assert.Equal("Default", tenant.Name);
        Assert.Equal("default", tenant.Slug);
        Assert.Equal(Now, tenant.CreatedAt);
    }

    [Fact]
    public async Task Migrating_twice_does_not_seed_a_second_tenant()
    {
        using var store = new TemporaryStore();

        await using (var first = store.CreateContext())
        {
            Assert.True(await new BookkeepingMigrator(first).MigrateAndSeedAsync("Default", "default", Now));
        }

        await using (var second = store.CreateContext())
        {
            Assert.False(await new BookkeepingMigrator(second).MigrateAndSeedAsync("Default", "default", Now));
        }

        await using var check = store.CreateContext();
        Assert.Equal(1, await check.Tenants.CountAsync());
    }

    [Fact]
    public async Task Migrating_creates_the_store_file_when_it_does_not_exist()
    {
        using var store = new TemporaryStore();
        Assert.False(File.Exists(store.Path_));

        await using (var context = store.CreateContext())
        {
            await new BookkeepingMigrator(context).MigrateAndSeedAsync("Default", "default", Now);
        }

        Assert.True(File.Exists(store.Path_));
    }
}
