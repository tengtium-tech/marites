# Cross-cutting platform behavior

Version 0.1 | 07 November 2026 | Status: Partial; index only, with the owning records linked.

Make reliable capture, identity, audit, configuration and process topology explicit without assigning more than the accepted decisions already do.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Capture path](../architecture/architecture-overview.md) (§7) | Consumer, `Channel<T>` buffer, batch, binary `COPY`, rollup upsert in one transaction, ack only after commit |
| [ADR-011, ADR-012, ADR-019](../architecture/decisions.md) | `COPY` over the ORM, day partitions, and the redelivery ledger that keeps counts exact |
| [Process topology](../architecture/architecture-overview.md) (§9) | One image, three entrypoints, queue-shard ownership by PostgreSQL advisory lock |
| [Identity and authorisation](../architecture/architecture-overview.md) (§8) | Entra ID with App Roles, capability policies, break-glass admin, `MARITES_AUTH_MODE` |
| [Audit](../data-and-contracts/data-model.md) | `operation_audit`, written only by the operations executor, never deleted automatically |
| [Configuration](../operations/deployment-and-onboarding.md) (§4) | Zero baked-in broker configuration; `MARITES_` variables; storage ceiling and partition interval set at deploy |
| [Multiple clusters](../architecture/architecture-overview.md) (§10) | A star of independent connections with bulkhead isolation (ADR-010) |

## Work still needed

Service metrics, traces and alerting remain unspecified; deployment documentation defines the log contract and health/readiness endpoints. The configuration reference and backup/recovery procedure now exist in operations. Key rotation for the credential encryption key remains unspecified. The capture path is drafted, not implemented or measured.

Use the [documentation structure](../project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
