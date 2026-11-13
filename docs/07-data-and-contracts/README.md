# Data and contracts

Version 0.4 | November 2026 | Status: Approved — Product owner, 2026-11-11

What is stored, what the counting means, and the HTTP contract itself.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Data model](data-model.md) | Thirteen current tables in two stores, with ownership and bookkeeping relationship diagrams, aggregate mapping, counting-versus-storing split, retention, storage ceiling and access strategy |
| [Data dictionary](data-dictionary.md) | Column-level types, nullability, defaults and constraints, a planned Outbox record, PostgreSQL-first message-store ERD and index list |
| [First migration plan](migration-plan.md) | Separate SQLite/PostgreSQL migration streams, partitioning, tenant isolation, post-migration checks, rollback, and named gaps |
| [Contract conventions](contract-conventions.md) | OpenAPI 3.2.1, QUERY semantics, outcome classes, idempotency, revision guard, confirmation, paging, versioning, security. Binding on the contract |
| [API catalog](api-catalog.md) | 25 operations (10 served, 15 deferred) with roles, capabilities, guards, HTTP methods and milestones |
| [Error catalog](error-catalog.md) | 28 codes with HTTP status, outcome class and remedy |
| [API architecture and NFRs](../05-architecture/api-architecture-and-nfrs.md) | Security, reliability, caching, performance, tooling, observability, and API verification rules |
| [Served contract](openapi/openapi.yaml) | OpenAPI 3.2.1, 10 operations; static lint passed 2026-11-11 |
| [Deferred contract](openapi/openapi.deferred.yaml) | OpenAPI 3.2.1, 15 designed operations not yet served; static lint passed 2026-11-11 |
| [Controlled vocabulary](../14-reference/vocabulary.md) | Every enumerated value the schema and contract use |

## Why the contract lives here

The contract is the product boundary, not a build artefact. The React client and the C# service are both measured against it. Where the contract and a convention disagree, the convention wins and the contract is corrected.

## The three rules most likely to be misread

1. **`published` and `deadLettered` are not additive.** A dead letter is a published message observed a second time.
2. **Retention affects rows, never counts.** Rollups are never rebuilt from rows.
3. **An unavailable granularity is an error, not an empty series.** Otherwise a retained gap renders as zero traffic.

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
