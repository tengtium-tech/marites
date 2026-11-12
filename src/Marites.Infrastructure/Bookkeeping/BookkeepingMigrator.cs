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
        await _context.Database.MigrateAsync(cancellationToken);

        if (await _context.Tenants.AnyAsync(cancellationToken))
        {
            return false;
        }

        _context.Tenants.Add(Tenant.Create(tenantName, tenantSlug, now));
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
