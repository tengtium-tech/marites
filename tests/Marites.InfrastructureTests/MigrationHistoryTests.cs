using Marites.Infrastructure.Bookkeeping;
using Microsoft.EntityFrameworkCore;

namespace Marites.InfrastructureTests;

/// <summary>
/// A store from an earlier migration set is refused with a message that names the cause.
/// </summary>
/// <remarks>
/// The migration set was regenerated before release. Without this check, migrating such a store
/// fails with `table "app_user" already exists` — an error that describes the symptom and hides
/// the cause, and which an operator cannot act on.
/// </remarks>
public sealed class MigrationHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_store_with_unknown_history_is_refused_with_the_reason()
    {
        using var store = new TemporaryStore();

        await using (var setup = store.CreateContext())
        {
            await new BookkeepingMigrator(setup).MigrateAndSeedAsync("Default", "default", Now);

            // Simulate the pre-regeneration set: a history row this build has never heard of.
            await setup.Database.ExecuteSqlRawAsync(
                "insert into __EFMigrationsHistory (MigrationId, ProductVersion) values ({0}, {1})",
                "20261112081912_0001_tenant",
                "10.0.10");
        }

        await using var context = store.CreateContext();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BookkeepingMigrator(context).MigrateAndSeedAsync("Default", "default", Now));

        Assert.Contains("0001_tenant", error.Message);
        Assert.Contains("predates", error.Message);

        // The remedy has to be in the message, not left to be inferred from a SQLite error.
        Assert.Contains("deleting the store", error.Message);
    }

    [Fact]
    public async Task A_store_from_this_build_migrates_normally()
    {
        using var store = new TemporaryStore();

        await using (var first = store.CreateContext())
        {
            Assert.True(await new BookkeepingMigrator(first).MigrateAndSeedAsync("Default", "default", Now));
        }

        await using var second = store.CreateContext();
        Assert.False(await new BookkeepingMigrator(second).MigrateAndSeedAsync("Default", "default", Now));
    }
}
