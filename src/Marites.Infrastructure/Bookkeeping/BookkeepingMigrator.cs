using Marites.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Marites.Infrastructure.Bookkeeping;

/// <summary>
/// Brings the bookkeeping store to a usable state: schema applied, and the one implicit tenant
/// that single-broker mode assumes (ADR-014).
/// </summary>
/// <remarks>
/// This is the separate migration step DEC-014 requires, not something the service does on
/// startup. DEC-014's original reason — the runtime credential holds no DDL — does not apply to
/// SQLite, which has no roles; the surviving reason does: a deploy that migrates at startup and
/// then fails serves a half-migrated schema, while a step that runs first stops before the
/// application starts.
/// </remarks>
public sealed class BookkeepingMigrator
{
    private readonly BookkeepingDbContext _context;

    public BookkeepingMigrator(BookkeepingDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Applies every pending migration and seeds the implicit tenant if the store has none.
    /// </summary>
    /// <returns><c>true</c> when this call created the tenant, <c>false</c> when one already existed.</returns>
    public async Task<bool> MigrateAndSeedAsync(
        string tenantName,
        string tenantSlug,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await EnsureHistoryIsRecognisedAsync(cancellationToken);

        await _context.Database.MigrateAsync(cancellationToken);

        if (await _context.Tenants.AnyAsync(cancellationToken))
        {
            return false;
        }

        _context.Tenants.Add(Tenant.Create(tenantName, tenantSlug, now));
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Refuses a store whose history this build does not recognise, before EF tries to apply a
    /// migration over a schema that already has the tables.
    /// </summary>
    /// <remarks>
    /// The migration set was regenerated into a single <c>0001_bookkeeping_schema</c> before
    /// release. A store created by the earlier per-table migrations therefore holds history rows
    /// that no longer exist, and EF's next step is to create tables that are already there — which
    /// surfaces as "table already exists", an error that describes the symptom and hides the cause.
    ///
    /// Nothing is deployed, so the remedy is to delete the store. That is worth saying out loud
    /// rather than leaving an operator to infer it from a SQLite error.
    /// </remarks>
    private async Task EnsureHistoryIsRecognisedAsync(CancellationToken cancellationToken)
    {
        var known = _context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        var unknown = applied.Where(migration => !known.Contains(migration)).ToList();

        if (unknown.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The bookkeeping store at this path was created by migrations this build no longer has: "
            + string.Join(", ", unknown)
            + ". The migration set was regenerated into a single 0001_bookkeeping_schema before release, "
            + "and this store predates it. Nothing is deployed, so deleting the store and re-running "
            + "migrate loses only local test profiles.");
    }
}
