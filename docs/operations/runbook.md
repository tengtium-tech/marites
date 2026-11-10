---
document_id: DOC-OPS-005
title: Runbook
product: Marites
version: 0.1
date: 2026-11-09
status: Planned — no alerting exists, so nothing here has been triggered
---

# DOC-OPS-005 — Runbook

**Source:** [component failure modes](../architecture/failure-modes.md); [deployment and onboarding](deployment-and-onboarding.md) §2; [data model](../data-and-contracts/data-model.md) §4. Owning document for what an operator does when something is wrong.

This product has **no alerting**. Every symptom below arrives because a person noticed, which means the runbook's first job is to work out whether the report is about Marites or about the broker it watches. Most of them are about the broker.

## 1. Before anything else

| Question | How to answer | If the answer is no |
|---|---|---|
| Is the API answering? | `curl -sS $MARITES_URL/api/v1/capabilities` | Check the container, then the database connection |
| Is the database reachable and has it room? | `SELECT pg_database_size('marites');` and free space on the volume | Free space first. A full disk stops capture and, past the ceiling, stops rows by design |
| Is capture advancing on any queue? | `SELECT max(captured_at) FROM message_capture;` | If that is minutes old on a busy system, go to §3 row 4 before reading anything else |

Three questions, thirty seconds. They separate "the product is broken" from "the product is working and the broker is not", which is the only distinction that changes what you do next.

## 2. Reading a queue's state

`captureState` is the field that tells you which of five situations you are in ([VOCABULARY §15](../reference/vocabulary.md)). Read it before reading any count.

| State | What it means | First thing to check |
|---|---|---|
| `live` | Capture bound and advancing | Nothing. Look at the counts |
| `stale` | Capture stopped advancing while depth grows, or the writer stalled | Worker logs, then database latency |
| `capped` | The storage ceiling was reached; rows paused, counts continue | Storage; whether the ceiling or a full disk is the cause |
| `disabled` | Capture was never enabled for this queue | Whether it should be |
| `unavailable` | Capture cannot be enabled — grant refused or entity gone | Preflight, then the broker |

## 3. Symptoms

| # | Symptom | Likely cause | Action | Who owns it |
|---|---|---|---|---|
| 1 | Dashboard is empty on first login | No broker profile, or preflight never passed | Run the setup wizard; read the failing check | Adopter |
| 2 | Every queue shows `0` | Capture is not enabled, or the queue list is not filtered to a profile with data | Check `captureState` first. `0` with `disabled` is not zero traffic | Adopter |
| 3 | Dead-letter tile says "unavailable" | The queue has no dead-letter routing | Configure `x-dead-letter-exchange` on **new** queues; existing queues need recreation ([DEC-004](../project/decisions-and-open-questions.md)) | Adopter |
| 4 | `stale` badge with depth rising | Worker stopped, database slow, or the network to the broker dropped | §4.1, then §4.2 | Product or environment — decide with §5 |
| 5 | `capped` badge | Storage ceiling reached | Raise the ceiling or reduce retention/payload policy. **Counts are still correct** | Adopter |
| 6 | Capture-gap warning | Shadow queue overflowed and dropped copies | Check `x-max-length` against real volume; raise the bound or reduce the firehose | Adopter |
| 7 | Preflight fails on `topology_write_permitted` | Missing `configure`/`write`/`read` on `marites.`, or `write` on the watched exchange | Grant it, or have the platform team pre-create the queue ([deployment §1.2](deployment-and-onboarding.md)) | Adopter |
| 8 | Preflight fails on `management_api_reachable` | Management plugin off, port blocked, or wrong credentials | Enable the plugin; check the network path **from the application**, not from a browser | Adopter |
| 9 | Preflight fails on `clock_skew` | Host clock drift | Fix NTP. Until then `x-death` timestamps are wrong, quietly | Adopter |
| 10 | Replay is `halted` | Target queue grew faster than the replay drained it | Fix the consumer, then resume explicitly. Do not resume into the same condition | Support |
| 11 | Replay or publish returned `202` with `Unresolved` | The operation failed partway; some messages were sent | Poll `/operations/{id}` and reconcile. **Do not retry blindly** ([ERR-OPR-004](../data-and-contracts/error-catalog.md)) | Support |
| 12 | `429` on an operation | Tenant rate limit | Wait for `Retry-After`. If it recurs, the operation is too large — reduce the batch | Support |
| 13 | `503` on any read | Database or broker unreachable | §4.1 | Environment |
| 14 | `401` on every request | Token expired, or Entra keys unreachable | Re-authenticate. If the key endpoint is unreachable, use break-glass ([DOC-ARCH-001 §8](../architecture/architecture-overview.md)) | Adopter |
| 15 | Audit list missing an operation | The operation failed before the executor ran, so nothing was executed | Check the application log for the request. If it *was* executed, this is a defect | Product |
| 16 | Partitions missing for today | Sweeper not running | Run `marites_ensure_partitions(7)`; then find out why the sweeper stopped | Product |
| 17 | Counts disagree between a day and its hours | Compaction wrote a partial or repeated aggregate | **Stop compaction**, snapshot the two tiers, and escalate. A wrong total is permanent once compacted | Product |
| 18 | Worker restart loop | Broker unreachable at startup, or a queue claim conflict | Check the advisory-lock owner: two workers claiming one queue serialise the rollup upsert (ADR-009) | Product |

**Row 17 is the one that needs a person, not a procedure.** It is the failure mode flagged as unspecified in [failure modes §6](../architecture/failure-modes.md): additive upserts plus a retried compaction would double a total. Until that guard is specified, the safe action is to stop compaction and preserve both tiers as evidence.

## 4. Diagnostics

### 4.1 Service and database

```sql
-- Is anything being captured at all, and how far behind is it?
SELECT q.name, q.capture_enabled, max(m.captured_at) AS last_row
FROM queue_ref q LEFT JOIN message_capture m ON m.queue_ref_id = q.id
GROUP BY q.id, q.name, q.capture_enabled ORDER BY last_row NULLS FIRST;

-- Storage by table, to see whether rows or rollups dominate.
SELECT relname, pg_size_pretty(pg_total_relation_size(relid))
FROM pg_catalog.pg_statio_user_tables ORDER BY pg_total_relation_size(relid) DESC LIMIT 10;

-- Partitions present, and the newest one.
SELECT inhrelid::regclass AS partition FROM pg_inherits
WHERE inhparent = 'message_capture'::regclass ORDER BY 1 DESC LIMIT 5;
```

### 4.2 The broker, independently of Marites

```bash
# Does the broker agree with what the dashboard shows?
rabbitmqctl list_queues name messages consumers message_stats.publish_details.rate
rabbitmqctl list_permissions -p <vhost>
rabbitmqctl list_queues name arguments | grep dead-letter
```

If the broker's own numbers match the dashboard, Marites is reporting correctly and the problem is upstream. That check settles most arguments about whether the monitoring tool is lying.

## 5. Product defect or environment?

| Evidence | Verdict |
|---|---|
| Broker shows messages; `message_capture` has none; `captureState` is `live` | **Product.** The state claims capture is advancing and the rows disagree |
| Broker shows messages; `captureState` is `unavailable` or `stale` | Environment, or a grant. The product is reporting honestly |
| Counts match the broker's publish counter | Product is correct; look at the consumer that is failing |
| Counts diverge from the broker by a growing margin | Capture gap. Either overflow (adopter's bound) or a stalled worker (product) |
| Preflight fails and the operator can fix the named grant | Environment |
| Preflight passes and capture still does not start | **Product** |

## 6. Do not

| Do not | Because |
|---|---|
| Delete partitions manually to free space | It destroys the only copy of those rows and changes no count. Use retention, which is designed for this |
| `UPDATE message_rollup` to "fix" a number | Totals are the record. An edited total has no provenance and will not match its hours |
| Delete or edit an audit row | It is the evidence that an operation happened. Deleting it does not undo the operation |
| Retry an `Unresolved` operation | Some messages were already sent. Reconcile through `/operations/{id}` first |
| Resume a `halted` replay without fixing the consumer | It will halt again, having sent more messages into a queue that cannot drain |
| Restart workers during a suspected double-count | Restarting does not stop the inflation; stop the writer, then investigate |

## 7. Gaps

| Gap | Consequence |
|---|---|
| No alerting or paging | Every symptom is discovered by a person. This is the largest operational gap in the product |
| No metrics or health endpoint | There is no way to watch Marites with Marites. The [platform foundation index](../platform-foundation/README.md) records this |
| No log format specification | Diagnostics in §4 assume structured logs exist; no format is specified |
| Thresholds unstated | "Minutes old" and "growing margin" are judgement, not values |

## 8. Document control

Revision 0.1 records eighteen symptoms with a first check and an action, plus the diagnostics that separate a product defect from an environment problem. Written 09 November 2026 to close the gap named in the [operations index](README.md). No symptom has been observed, because nothing runs.
