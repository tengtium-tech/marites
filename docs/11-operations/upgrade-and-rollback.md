---
document_id: DOC-OPS-003
title: Upgrade and Rollback
product: Marites
version: 0.1
date: 2026-11-09
status: Planned — no released version exists
---

# DOC-OPS-003 — Upgrade and Rollback

**Source:** [deployment and onboarding](deployment-and-onboarding.md) §3, §6; [migration plan](../07-data-and-contracts/migration-plan.md). Owning document for how a running install moves between versions, and what happens when it has to move back.

No version of Marites has been released, so nothing below has been executed. The procedure is written now because the first upgrade will happen under pressure, and a rollback invented during an incident is a rollback that loses data.

## 1. What a release is

| Component | Identifier | Changed by |
|---|---|---|
| Application | Container image tag (`marites:1.4.2`) | Replacing the image and restarting |
| Chart | Helm chart version, pinned to an image tag | `helm upgrade` |
| Schema | EF Core migration set, applied at start or by a job | Running the migrations |
| Contract | Major version in the path (`/api/v1`) | Only a breaking change |

Four components, and the only one that is hard to reverse is the third.

## 2. Before upgrading

| Check | Why |
|---|---|
| A restore-tested backup exists, taken after the last write quiesced | An untested backup is a hope, not a backup ([backup and recovery](backup-and-recovery.md)) |
| Read the migration list and classify each step | Additive steps are reversible by rolling the application back; anything that drops, renames or narrows is not |
| Free space is above the storage ceiling plus one retention window | A migration that rewrites a table needs room beside the existing copy |
| Partition headroom covers the upgrade window | If the sweeper is stopped during the upgrade, capture stalls when the last partition fills |
| The capability record diff is understood | A new version may read a provider differently; a changed capability changes which controls appear |

The second row is the one that decides the plan. **Additive migrations can be rolled back by deploying the previous image. Destructive ones cannot**, and knowing which you have before starting is the difference between a rollback and a restore.

## 3. Order of operations

1. **Expand the schema.** Apply only additive steps: new nullable columns, new tables, new indexes. The previous version must run against the new schema without noticing it.
2. **Deploy the new application.** Rolling update; capture workers restart one shard at a time so the other shards keep consuming.
3. **Verify.** Contract conformance (TC-001), one queue's metadata against the broker, and one rollup window against its stored values.
4. **Contract.** If the release changes an optional field, publish the new contract and regenerate the client. A major change requires `/api/v2` and is out of scope for a routine upgrade.
5. **Contract the schema.** Drop the now-unused columns or tables in the *next* release, not this one — after the new version has run long enough that a rollback is no longer plausible.

**Never contract in the same release that expands.** A release that adds a column and drops another has no rollback: the previous image needs the dropped column, and the data is gone.

## 4. Rollback

| Scenario | Action | Data consequence |
|---|---|---|
| New version fails at startup, schema additive | Redeploy the previous image. Nothing else | None |
| New version runs but behaves wrongly, schema additive | Redeploy the previous image; keep the new columns, which the old version ignores | None |
| A migration dropped or narrowed a column | **Restore from backup.** The previous image cannot read the new schema | Everything written since the backup is lost |
| Capture is double-counting after an upgrade | Stop the workers first, then roll back. A rollback that leaves workers running keeps inflating counts | Counts written while faulty are permanent — rollups are never rebuilt (ADR-007) |
| The upgrade changed the shadow-queue naming or bounds | Rolling back orphans the new queues; remove them explicitly | Adopter's topology unaffected |

**The fourth row is the one to remember.** Rollups are additive and never rebuilt, so an over-count during a faulty release cannot be corrected by deploying the previous version. Stopping the writer is the only control that exists.

## 5. What cannot be rolled back

| Action | Why it is irreversible |
|---|---|
| Dropped partitions | The rows are the only copy. Retention is by design; there is no archive |
| A purge | The messages were removed from the adopter's broker. Marites did what it was told |
| A published or replayed message | It is in a business flow. A rollback of Marites does not recall it |
| Audit rows | Append-only; no endpoint and no procedure deletes one |
| Rollup totals | Never rebuilt from rows, so a wrong total survives every rollback |

This table is the reason the guard chain exists ([DOC-UX-001 §3](../08-experience/dashboard-and-operations.md)). None of these operations is reversible, so all of them are confirmed, dry-run and audited before they happen.

## 6. Versioning rules

- **Image tag** is the deployment identity. `latest` is not a version and is not supported for production.
- **Chart version** is pinned to an image tag so a chart upgrade cannot silently change the application.
- **Contract major** changes only for a breaking change, and v1 keeps working until formally retired ([DOC-API-001 §7](../07-data-and-contracts/contract-conventions.md)).
- **Migration identifiers** are never renamed. A migration that has shipped is a fact; a renamed one makes two installs disagree about what is applied.

## 7. Gaps

| Gap | Consequence |
|---|---|
| No zero-downtime procedure | Every step assumes a restart window. Expand/contract is described but not exercised, and no blue-green or canary procedure exists |
| No migration dry run | There is no "apply and roll back in a scratch database" step, which is the cheapest way to find a destructive migration |
| No version-compatibility matrix | Which application versions run against which schema versions is unstated beyond "additive is compatible" |
| No downgrade test | The rollback paths are reasoned, not tested. Nothing has been rolled back because nothing has been released |

## 8. Document control

Revision 0.1 records the upgrade and rollback procedure, written 09 November 2026 to close the gap named in the [operations index](README.md). It describes an intent for software that does not exist; no upgrade has been performed and no rollback has been tested.
