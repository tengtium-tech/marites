---
document_id: DOC-DATA-003
title: First Migration Plan
product: Marites
version: 0.1
date: 2026-11
status: Planned — no schema exists and nothing has been applied
---

# DOC-DATA-003 — First Migration Plan

**Source:** [data model](data-model.md), [data dictionary](data-dictionary.md). Owning document for the order in which the schema is created and what has to be true after it is.

This is a plan, not a migration. No database has been created, no DDL has been executed, and no PostgreSQL version has been chosen beyond "16 or later". Everything below is `Planned`.

## 1. Preconditions

| Item | Requirement | Why |
|---|---|---|
| PostgreSQL | 16 or later | Native partitioning, `gen_random_uuid()`, `jsonb` subscripting, and `MERGE` behaviour on upserts |
| Extensions | `pgcrypto` (for `gen_random_uuid()` on older builds), `pg_trgm` (text search at M3) | `pg_trgm` is created at M3, not now — an unused extension is a permission request with no benefit |
| Roles | A **migration** role for DDL and a **runtime** role for DML | The runtime role must not own the tables, or `ALTER` rights become ambient. The exact grants per engine are in [storage prerequisites](../11-operations/storage-prerequisites.md) |
| Database | `marites`, UTF-8, UTC | Storing local time is the defect this avoids |
| Storage | Sized for `retention × daily volume` plus rollups | Rollups are single-digit gigabytes for a hundred queues over three years (data model §3.1) |

**One database for all clusters** (ADR-010). A per-cluster database would defeat the reason people want one dashboard.

## 2. Migration order

Each step is a separate EF Core migration so a failure leaves a known prefix applied. The order is dependency order, not importance order.

| Step | Creates | Notes |
|---|---|---|
| `0001_tenant` | `tenant` | Must exist before anything is tenant-scoped |
| `0002_identity` | `app_user` | Break-glass row is created by the application on first start, not by the migration — a migration that inserts a user is a migration with a password in it |
| `0003_broker_profile` | `broker_profile` | Includes `revision`, `credentials_updated_at`, `capabilities_json` |
| `0004_queue_ref` | `queue_ref` | Depends on `broker_profile` |
| `0005_capture_rule` | `capture_rule` | Unique on `queue_ref_id` |
| `0006_message_rollup` | `message_rollup` | Natural primary key, no surrogate |
| `0007_message_capture` | `message_capture` parent, partitioned | See §3; the parent is created empty and every partition is a separate DDL statement |
| `0008_capture_dedupe` | `capture_dedupe` | ADR-019 ledger |
| `0009_replay_job` | `replay_job` | Depends on `queue_ref` and `app_user` |
| `0010_operation_audit` | `operation_audit` | Append-only; the runtime role gets `INSERT` and `SELECT` only, never `UPDATE` or `DELETE` |
| `0011_indexes` | Every index in the dictionary §11 | Created after load-free table creation, which is the only time it is cheap |
| `0012_row_level_security` | Policies on configuration tables | See §4 |
| `0013_operation_payload` | `operation_payload` and its two indexes | DEC-017. Created before the audit screen ships, because an audit row without its evidence is a claim with no proof |
| `0014_operation_intent` | `operation_intent` and its reconciliation index | ADR-023. The durable hold must exist before any destructive operation is served: without it, a command whose bodies were never written has nothing to record |

**Why `message_capture` comes seventh rather than first.** It is the table that matters, and it is also the only one that cannot be created and forgotten. Putting it after the configuration tables means the partitioning and index statements are written against a schema whose dependencies already exist, rather than being debugged while five other tables are missing.

## 3. Partitioning

`message_capture` is `PARTITION BY RANGE (captured_at)`, one partition per day, named `message_capture_pYYYYMMDD`.

1. Create the parent with no partitions.
2. Create the current day and the next seven days explicitly, so a fresh install can capture immediately.
3. Install a function `marites_ensure_partitions(days_ahead int)` that creates missing partitions idempotently.
4. The partition sweeper calls it on every pass, then drops partitions whose whole range is older than the tightest row retention in `capture_rule`.

**The sweeper must read the minimum retention across all rules, not a global setting.** Row retention is per queue (DEC-005); dropping a partition because one queue has a 24-hour retention would delete another queue's 72-hour window.

**Dropping a partition is irreversible.** There is no soft delete and no archive. That is the intended trade: rows are the investigation window, not the record (data model §3.1.1).

## 4. Row-level security

| Table | Policy | Rationale |
|---|---|---|
| `broker_profile`, `queue_ref`, `capture_rule`, `replay_job`, `app_user`, `operation_audit` | `USING (tenant_id = current_setting('marites.tenant_id')::uuid)` | Configuration is read and written per request, so the tenant context is always set |
| `message_rollup`, `message_capture`, `capture_dedupe` | **No policy at M0–M3** | The worker writes across tenants in one batch; a per-row policy would require setting the tenant per batch and would make the `COPY` path unusable. Access is constrained by tenant-scoped keys and by the API's query shape |

That asymmetry is a decision, and it is the weakest point in the isolation story. It is recorded as a risk in the [threat model](../09-security/threat-model.md) (THR-020) and it is why TC-022 exists. Moving capture tables to RLS requires either a per-tenant `COPY` or a `SECURITY DEFINER` writer — both change the write path, so it is not a migration-time decision to make casually.

## 5. Verification after applying

| Check | Expected |
|---|---|
| `\dt` | Twelve tables — nine in Marites' own store, three in the message store — plus one partition per day in range |
| `\d+ message_capture` | `Partitioned table`, range on `captured_at` |
| Insert a row with a `captured_at` outside every partition | Error naming the missing partition — proves partitioning is active rather than decorative |
| Insert two rows with the same `(queue_ref_id, granularity, bucket_start)` | Second is an upsert, not a duplicate |
| `UPDATE operation_audit` as the runtime role | Permission denied |
| `SELECT` from `broker_profile` with no tenant context set | Error, not a full table read |
| Drop a partition, then read the rollup for that period | Totals unchanged (TC-015) |

The fourth check is the one that catches a natural key implemented as a surrogate. The sixth catches an RLS policy that was enabled and never forced.

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

Revision 0.1 records the first migration plan, written November 2026 to close the gap named in the [data-and-contracts index](README.md). Nothing has been applied. The three items in §8 are operational procedures that a released version makes necessary, not unspecified behaviour: the object-store schema was specified in the [data dictionary](data-dictionary.md) §6 and is no longer among them.
