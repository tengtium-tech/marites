using Marites.Application.Audit;
using Marites.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Marites.Infrastructure.Bookkeeping;

/// <summary>EF adapter for the append-only audit.</summary>
public sealed class AuditLog : IAuditLog
{
    private readonly BookkeepingDbContext _context;

    public AuditLog(BookkeepingDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(OperationAudit record, CancellationToken cancellationToken)
    {
        _context.OperationAudits.Add(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationAudit>> ReadAsync(
        Guid tenantId,
        int take,
        CancellationToken cancellationToken) =>
        await _context.OperationAudits
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}
