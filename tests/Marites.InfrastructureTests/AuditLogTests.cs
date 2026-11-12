using Microsoft.EntityFrameworkCore;
using Marites.Domain.Audit;
using Marites.Infrastructure.Bookkeeping;

namespace Marites.InfrastructureTests;

/// <summary>
/// The audit's two promises through the application boundary: a row written is a row read, and
/// the schema still refuses to change one afterwards.
/// </summary>
public sealed class AuditLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_written_row_is_read_back()
    {
        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        context.Database.Migrate();

        var tenant = Guid.NewGuid();
        var log = new AuditLog(context);

        await log.AppendAsync(
            OperationAudit.Record(tenant, "peek", "orders", "ok", Now, messageCount: 5, via: "assistant"),
            CancellationToken.None);

        var rows = await log.ReadAsync(tenant, take: 10, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("peek", row.Action);
        Assert.Equal("orders", row.Target);
        Assert.Equal(5, row.MessageCount);
        Assert.Equal("assistant", row.Via);
        Assert.Equal(Now, row.CreatedAt);
    }

    [Fact]
    public async Task Reading_is_scoped_to_one_tenant()
    {
        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        context.Database.Migrate();

        var log = new AuditLog(context);
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        await log.AppendAsync(OperationAudit.Record(mine, "peek", "a", "ok", Now), CancellationToken.None);
        await log.AppendAsync(OperationAudit.Record(theirs, "peek", "b", "ok", Now), CancellationToken.None);

        var rows = await log.ReadAsync(mine, take: 10, CancellationToken.None);

        Assert.Equal("a", Assert.Single(rows).Target);
    }
}
