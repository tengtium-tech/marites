---
document_id: DOC-OPS-004
title: Backup and Recovery
product: Marites
version: 0.1
date: 2026-11-09
status: Planned — no backup has been taken, no restore has been tested
---

# DOC-OPS-004 — Backup and Recovery

**Source:** [data model](../data-and-contracts/data-model.md) §3–§4; [migration plan](../data-and-contracts/migration-plan.md). Owning document for what must be backed up, what cannot be recovered, and how a restore is proven.

The uncomfortable property of this product is that **the most valuable data is the cheapest to lose by accident and the hardest to lose by design.** Counts are small and must never be lost; rows are large and are expected to expire. A backup plan that treats them the same gets one of the two wrong.

## 1. What must be backed up

| Item | Size | Loss consequence | Frequency |
|---|---|---|---|
| `message_rollup` | Single-digit GB for a hundred queues over three years | **Every historical count is gone.** Not reconstructible from anything | Same as the rest of the database, with a short RPO |
| Configuration tables (`tenant`, `broker_profile`, `queue_ref`, `capture_rule`, `app_user`) | Megabytes | The install must be reconfigured from scratch, including re-entering every credential | Daily, or after any change |
| `operation_audit` | Grows with operations, not traffic | The evidence of who did what is gone. Not reconstructible | Daily |
| `message_capture` rows | Large, bounded by retention | The investigation window is lost; counts survive | **Optional.** Restoring rows is rarely worth the restore time |
| Object-store spillover | Proportional to spilled payloads | Payloads above 64 KB are unreadable | Only if rows are restored |
| Key-encryption key (KEK) | Not in the database | **Every stored credential becomes undecryptable.** The install still runs, but no broker profile works | Managed by env or KMS, **not by this backup** |

**The last row is the trap.** A database restore into an environment with a different KEK yields a working product with unreadable credentials, and the error looks like a credential fault rather than a key mismatch. The KEK must be backed up by whatever mechanism holds it, and its recovery must be tested separately from the database's.

## 2. What cannot be recovered

| Item | Why |
|---|---|
| Rows older than the retention window | Dropped with their partition. There is no archive and no soft delete |
| Rollups lost before the last backup | Never rebuilt from rows (ADR-007), so the gap is permanent |
| Purged messages | Removed from the adopter's broker by an explicit human action |
| A published or replayed message | It is in a business flow |
| Audit rows deleted outside the product | No endpoint can delete them; a direct SQL delete is outside every guarantee this document makes |

## 3. Proposed targets

No target has been agreed with anyone, because there is no adopter yet. The values below are **proposed defaults**, chosen so that a restore loses at most a day of counts and the install is usable within a working session. They are not commitments.

| Objective | Proposed | Rationale |
|---|---|---|
| RPO — counts and configuration | 1 hour (WAL archiving), 24 hours (logical dump) | An hour of counts lost is recoverable by explanation; a day is not |
| RPO — captured rows | 24 hours, or not at all | Rows are an investigation window; losing a day is an inconvenience, not a data loss |
| RTO — service usable | 4 hours | Restore the database, redeploy the image, re-validate credentials |
| RTO — full history | 24 hours | Restoring captured rows is slow and usually skipped |

**Skipping the rows is a legitimate choice and should be stated as one.** An adopter who restores configuration and rollups only, accepting the loss of the investigation window, has made a sound decision. An adopter who does not know the choice exists has not made one.

## 4. Procedure

### 4.1 Taking a backup

```bash
# Logical dump: configuration, rollups and audit. Rows are included unless excluded.
pg_dump --format=custom --compress=9 \
  --exclude-table-data='message_capture*' \
  --file=marites-$(date +%Y%m%dT%H%M).dump marites

# Physical backup plus WAL archiving, where point-in-time recovery is required.
pg_basebackup -D /backup/base -Ft -z -X stream
```

Excluding `message_capture` data is the default because it dominates the dump and is the least valuable part. Including it is a deliberate choice for an adopter who wants a full forensic copy.

### 4.2 Restoring

1. **Provision PostgreSQL** at the same major version. A restore into a different major version is a migration, not a restore.
2. **Restore the dump** into an empty database named `marites`.
3. **Restore the KEK** into the environment before starting the application. Starting without it produces a running product whose every credential fails.
4. **Recreate the partition set.** A restore brings the parent and whatever partitions the dump contained; the sweeper creates the missing days on its first pass.
5. **Verify** with the checks in §5.
6. **Re-run preflight** on every broker profile. Credentials may have rotated in the broker while the backup was the source of truth.

### 4.3 Verifying a restore

| Check | Expected |
|---|---|
| Row counts per table | Match the source at the backup time, within the excluded tables |
| `SELECT sum(published) FROM message_rollup` | Matches the source total for the same window |
| One queue's day total versus its 24 hour rows | Equal — the compaction invariant survived the restore |
| A broker profile's preflight | Passes, proving the KEK and the credential decrypted together |
| The audit list for a known operation | Present, with the same actor and target |
| Partitions for the current day | Exist, or are created by the sweeper on first pass |

**The third check is the one people skip.** A restore that loses the compaction invariant — a day row that no longer equals the sum of its hours — produces a dashboard whose weekly total disagrees with its daily totals, and nothing on screen reveals which is wrong.

## 5. Test cadence

| Test | Frequency | Pass condition |
|---|---|---|
| Restore into a scratch environment | Quarterly, and before any upgrade that touches `message_rollup` | §4.3 checks all pass |
| KEK recovery | Annually, and whenever the key custodian changes | A restored profile decrypts and preflights |
| Object-store restore | Once, before M3 ships payload inspection | A spilled payload opens byte-identical to the source |

An untested restore is not a backup. That sentence is the whole reason this document exists, and the quarterly test is the only part of it that has any value if the rest is ignored.

## 6. Gaps

| Gap | Consequence |
|---|---|
| No key-rotation procedure | A suspected KEK compromise has no documented response. Rotation would require re-encrypting every credential, and the path is unwritten |
| No object-store lifecycle | Spilled payloads have no stated retention or deletion rule, so a backup of them has no stated scope |
| No automated backup verification | The restore test is manual and quarterly. Nothing checks that last night's backup is restorable |
| No off-host storage requirement | Where backups live is unstated. A backup on the same volume as the database is not a backup |

## 7. Document control

Revision 0.1 records what must be backed up, what cannot be recovered, proposed objectives and a restore procedure with its verification checks. Written 09 November 2026 to close the gap named in the [operations index](README.md). No backup has been taken, no restore has been performed, and the proposed targets in §3 are the author's defaults rather than an agreed requirement.
