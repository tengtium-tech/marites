---
document_id: DOC-DATA-003
title: First Migration Plan
product: Marites
version: 0.1
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-DATA-003 — First Migration Plan

**Source:** [data model](data-model.md), [data dictionary](data-dictionary.md). Owning document for the order in which the schema is created and what has to be true after it is.

This is a plan, not a migration. No database has been created, no DDL has been executed, and no PostgreSQL version has been chosen beyond "16 or later". Bookkeeping SQLite and the adopter's message store have separate migration streams. Everything below is `Planned`.

## 1. Preconditions

| Item | Requirement | Why |
|---|---|---|
| PostgreSQL | 16 or later | Native partitioning, `gen_random_uuid()`, `jsonb` subscripting, and `MERGE` behaviour on upserts |
| Extensions | `pgcrypto` (for `gen_random_uuid()` on older builds), `pg_trgm` (text search at M3) | `pg_trgm` is created at M3, not now — an unused extension is a permission request with no benefit |
| Roles | A **migration** role for DDL and a **runtime** role for DML | The runtime role must not own the tables, or `ALTER` rights become ambient. The exact grants per engine are in [storage prerequisites](../11-operations/storage-prerequisites.md) |
| Database | `marites`, UTF-8, UTC | Storing local time is the defect this avoids |
| Storage | Sized for `retention × daily volume` plus rollups | Rollups are single-digit gigabytes for a hundred queues over three years (data model §3.1) |

**One database for all clusters** (ADR-010). A per-cluster database would defeat the reason people want one dashboard.

## 2. Migration streams

The two stores have separate migration histories, credentials, engines, and transactions. A migration in one store cannot refer to a foreign key in the other. The table prefixes below are logical plan names; the concrete migration tool remains an implementation choice.

### 2.1 Bookkeeping SQLite

Marites creates this store. Its ten current records are created in dependency order:

| Step | Creates | Notes |
|---|---|---|
| `0001_bookkeeping_schema` | `tenant`, `app_user`, `broker_profile`, `queue_ref`, `capture_rule`, `replay_job`, `operation_audit`, `operation_payload`, `operation_intent`, `idempotency_key` | One migration, ten tables, created in dependency order. `operation_audit` also carries the append-only triggers |

**One migration, not ten.** The steps above were planned as separate files, but EF orders migrations by
timestamp rather than by name: the first implementation skipped the tables that did not exist yet and
numbered the later ones by their eventual position, which meant the missing tables would have applied
*after* them. With nothing deployed, the set was regenerated as a single migration that creates every
table in dependency order. The order above is the order inside the migration, and it is the order that
matters — a separate file per table bought nothing that a comment does not.

Step 11 remains conditional until a subscriber/sink is selected. The dispatcher leases pending rows, delivers at least once, retries transient failures with bounded backoff, and retains poison events for operator action. The Outbox does not replace `operation_intent` and no event is published until the sink is configured.

### 2.2 Adopter message store

The adopter creates the database and grants separate migration/runtime roles. PostgreSQL is the first supported engine; SQL Server is a separate later milestone (ADR-022). The PostgreSQL schema contains only the three traffic tables:

| Step | Creates | Notes |
|---|---|---|
| `message/0001_message_rollup` | `message_rollup` | Natural primary key, no surrogate |
| `message/0002_message_capture` | `message_capture` partitioned parent | See §3; create parent empty, then each partition explicitly |
| `message/0003_capture_dedupe` | `capture_dedupe` | ADR-019 ledger; write shares the capture batch transaction |
| `message/0004_message_indexes` | Dictionary indexes | Created after load-free table creation where possible |

Do not add tenant, broker, role, audit, or operation-intent tables to this store. Those remain in Marites' SQLite bookkeeping store. No cross-store migration or foreign key is possible.

The message-store migration creates the `message_capture` parent before its partitions. The first daily partitions must exist before capture begins; the partition-maintenance function then creates future partitions idempotently. Keeping this stream separate from bookkeeping migrations makes its partition prerequisites explicit and avoids implying cross-store dependencies.

## 3. Partitioning

`message_capture` is `PARTITION BY RANGE (captured_at)`, one partition per day, named `message_capture_pYYYYMMDD`.

1. Create the parent with no partitions.
2. Create the current day and the next seven days explicitly, so a fresh install can capture immediately.
3. Install a function `marites_ensure_partitions(days_ahead int)` that creates missing partitions idempotently.
4. The partition sweeper calls it on every pass, then drops partitions whose whole range is older than the tightest row retention in `capture_rule`.

**The sweeper must read the minimum retention across all rules, not a global setting.** Row retention is per queue (DEC-005); dropping a partition because one queue has a 24-hour retention would delete another queue's 72-hour window.

**Dropping a partition is irreversible.** There is no soft delete and no archive. That is the intended trade: rows are the investigation window, not the record (data model §3.1.1).

## 4. Tenant isolation

The bookkeeping store is SQLite and does not use PostgreSQL row-level security. Every Application repository method receives an authenticated tenant scope; callers cannot pass an arbitrary tenant ID to widen access. Repository queries must constrain that scope before mapping results.

| Store/table | Policy | Rationale |
|---|---|---|
| Bookkeeping tables | Application/repository tenant scope; no PostgreSQL RLS | These records live in Marites-owned SQLite, not the adopter PostgreSQL store |
| `message_rollup`, `message_capture`, `capture_dedupe` | **No RLS at M0–M3** | The worker writes across tenants in one batch; a per-row policy would require setting tenant per batch and would make the `COPY` path unusable. API projections are tenant-scoped in the query shape. |

This is the weakest point in the isolation story and remains THR-020 / TC-022. Moving the message tables to RLS requires either a per-tenant `COPY` or a `SECURITY DEFINER` writer; both change the write path and require a separate decision.

## 5. Verification after applying

| Check | Expected |
|---|---|
| Inspect SQLite schema | Ten current bookkeeping tables; eleven only after the conditional Outbox migration |
| PostgreSQL `\dt` | Three message-store parent tables plus range partitions; no bookkeeping tables |
| PostgreSQL `\d+ message_capture` | Partitioned table, range on `captured_at` |
| Insert a row outside every `captured_at` partition | Error naming the missing partition |
| Insert the same rollup key twice | Second write upserts rather than duplicates |
| `UPDATE operation_audit` through the runtime repository | Refused; audit is append-only |
| Request another tenant's broker/queue/message record | Repository/API refuses access; no cross-tenant result |
| Drop a message partition, then read the rollup for that period | Totals unchanged (TC-015) |
| Commit aggregate plus Outbox event, then interrupt dispatcher | Aggregate and event both persist; later delivery is retried and consumers deduplicate by event ID |

The rollup-key check catches a natural key implemented as a surrogate. The tenant-isolation check catches an Application query that is missing authenticated scope.

## 6. Rollback

| Situation | Action |
|---|---|
| Migration fails partway | Fix forward. EF Core migrations are not transactional across steps on all operations, and a partially applied schema is normal during development |
| A migration must be undone before any data exists | Drop in reverse dependency order; there is nothing to preserve |
| A migration must be undone after capture has run | **Not supported.** Partitions hold the only copy of captured rows, and the rollup cannot be rebuilt from them once dropped. Restore from backup or accept the loss |
| A release must be rolled back | The application rolls back, the schema does not — see [upgrade and rollback](../11-operations/upgrade-and-rollback.md) |

**Forward-only is a property of the data, not a preference.** Rollups are never rebuilt from rows (ADR-007), so there is no path that reconstructs counts after a destructive schema change. Every migration that touches `message_rollup` is one-way.

## 7. What this migration deliberately does not do

- **No database `ENUM` types.** Enumerated values are `text` plus a check constraint, because amending a Postgres enum needs a migration while the vocabulary is amended by prose first (data dictionary, preamble).
- **No triggers.** Every write path is explicit; a trigger is a second place a rule can live.
- **No cross-cluster uniqueness constraint.** Dedupe is out of scope for v1 (DEC-008), and an index created for a feature that does not exist is a write cost with no reader.
- **No seed data.** Sample topologies would be invented, and an invented fixture tests the fixture.
- **No soft-delete columns.** `message_capture` rows are dropped with their partition; `operation_audit` is never deleted.

## 8. Gaps

| Gap | Consequence |
|---|---|
| Online index creation | Indexes are created on empty tables here, which is free. A later index on `message_capture` at volume will need `CREATE INDEX CONCURRENTLY` outside a transaction — no procedure exists |
| Zero-downtime migration | Every step assumes the service can stop. No expand/contract pattern is documented, because there is no released version to upgrade from |
| Retention floor enforcement | The "rollup retention ≥ row retention" guard is application code, not a constraint. A direct SQL update could violate it |

## 9. Document control

Revision 0.1 records the first migration plan, written November 2026 to close the gap named in the [data-and-contracts index](README.md). Nothing has been applied. The three items in §8 are operational procedures that a released version makes necessary; they are not unspecified table definitions.
