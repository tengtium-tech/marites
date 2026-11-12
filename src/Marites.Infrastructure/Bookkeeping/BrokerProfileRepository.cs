using Marites.Application.Brokers;
using Marites.Domain.Brokers;
using Marites.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Marites.Infrastructure.Bookkeeping;

/// <summary>EF Core adapter for the broker profile port.</summary>
public sealed class BrokerProfileRepository : IBrokerProfileRepository
{
    private readonly BookkeepingDbContext _context;

    public BrokerProfileRepository(BookkeepingDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Single-broker mode has exactly one tenant, seeded by <c>marites migrate</c> (ADR-014).
    /// This is the one place that assumption lives, so replacing it with a claim lookup later
    /// is a change here rather than a search through handlers.
    /// </summary>
    public Task<Guid> CurrentTenantIdAsync(CancellationToken cancellationToken) =>
        _context.Tenants.Select(t => t.Id).FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<BrokerProfile>> ListAsync(
        string? environment,
        string? providerKind,
        CancellationToken cancellationToken)
    {
        var query = _context.BrokerProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(p => p.Environment == environment);
        }

        if (!string.IsNullOrWhiteSpace(providerKind))
        {
            query = query.Where(p => p.ProviderKind == providerKind);
        }

        return await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public Task<BrokerProfile?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _context.BrokerProfiles.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken cancellationToken) =>
        _context.BrokerProfiles.AnyAsync(
            p => p.Name == name.Trim() && p.Id != exceptId,
            cancellationToken);

    public void Add(BrokerProfile profile) => _context.BrokerProfiles.Add(profile);

    public void Remove(BrokerProfile profile) => _context.BrokerProfiles.Remove(profile);

    public Task SaveAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Reads and writes recorded command outcomes, so a retried <c>Idempotency-Key</c> replays the
/// first answer instead of applying the command twice (contract conventions §3.1).
/// </summary>
public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly BookkeepingDbContext _context;

    public IdempotencyStore(BookkeepingDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyKey?> FindAsync(
        string key,
        string operation,
        CancellationToken cancellationToken)
    {
        var tenantId = await CurrentTenantIdAsync(cancellationToken);

        return await _context.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.Key == key && r.Operation == operation,
                cancellationToken);
    }

    public async Task RecordAsync(
        string key,
        string operation,
        string requestHash,
        int responseStatus,
        string responseJson,
        CancellationToken cancellationToken)
    {
        var tenantId = await CurrentTenantIdAsync(cancellationToken);

        _context.IdempotencyKeys.Add(IdempotencyKey.Record(
            tenantId, key, operation, requestHash, responseStatus, responseJson, DateTimeOffset.UtcNow));

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Guid> CurrentTenantIdAsync(CancellationToken cancellationToken) =>
        _context.Tenants.Select(t => t.Id).FirstAsync(cancellationToken);
}
