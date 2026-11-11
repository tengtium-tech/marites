# Cross-cutting platform behavior

Make reliable capture, identity, audit, configuration and process topology explicit without assigning more than the accepted decisions already do.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Capture path](../05-architecture/architecture-overview.md) (§7) | Consumer, `Channel<T>` buffer, batch, binary `COPY`, rollup upsert in one transaction, ack only after commit |
| [ADR-011, ADR-012, ADR-019](../05-architecture/decisions.md) | `COPY` over the ORM, day partitions, and the redelivery ledger that keeps counts exact |
| [Process topology](../05-architecture/architecture-overview.md) (§9) | One image, three entrypoints, queue-shard ownership by PostgreSQL advisory lock |
| [Identity and authorisation](../05-architecture/architecture-overview.md) (§8) | Entra ID with App Roles, capability policies, break-glass admin, `MARITES_AUTH_MODE` |
| [Audit](../07-data-and-contracts/data-model.md) | `operation_audit`, written only by the operations executor, never deleted automatically |
| [Configuration](../11-operations/deployment-and-onboarding.md) (§4) | Zero baked-in broker configuration; `MARITES_` variables; storage ceiling and partition interval set at deploy |
| [Multiple clusters](../05-architecture/architecture-overview.md) (§10) | A star of independent connections with bulkhead isolation (ADR-010) |

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
