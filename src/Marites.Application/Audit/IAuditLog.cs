using Marites.Domain.Audit;

namespace Marites.Application.Audit;

/// <summary>
/// The append-only audit. There is no update and no delete on this port, because there is no
/// update and no delete in the schema: the table carries triggers that refuse both (FR-016).
/// A port that offered them would be a port an implementation could satisfy and the database
/// would refuse.
/// </summary>
public interface IAuditLog
{
    Task AppendAsync(OperationAudit record, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationAudit>> ReadAsync(Guid tenantId, int take, CancellationToken cancellationToken);
}
