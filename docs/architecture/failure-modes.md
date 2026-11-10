---
document_id: DOC-ARCH-002
title: Component Failure Modes
product: Marites
version: 0.1
date: 2026-11-09
status: Draft — designed behaviour; nothing implemented or observed
---

# DOC-ARCH-002 — Component Failure Modes

**Source:** [architecture overview](architecture-overview.md) §7, §9; [data model](../data-and-contracts/data-model.md) §4; [requirements](../business/requirements.md) FR-018, NFR-002. Owning document for what each component does when it fails.

A monitoring product fails in one distinctive way: **it stops monitoring and nobody notices.** Every row below is written to answer two questions — how does the failure become visible, and what does the operator see while it is happening. Where the answer is "nothing", the row says so.

## 1. The failure that must not exist

| Failure | Why it is unacceptable | Where it is prevented |
|---|---|---|
| Capture stops while the queue is busy, and the dashboard shows a plausible number | A quiet dashboard and a healthy system look identical. Everything else in this document is secondary to this | `captureState`, last-seen timestamp, and the published-versus-observed comparison (FR-018) |
| A count is reported as exact when a copy was dropped | The executive number is the product's output; a wrong one is worse than a missing one | Capture-gap warning; `captureState = capped` or `stale`; ADR-019 |
| A destructive operation runs without a human | Unrecoverable, and the product would be the cause of the incident | NFR-004; no scheduled destructive operation |

## 2. Management-API poller

Polls each broker profile for queue depths, rates, consumers and DLX presence.

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| Broker unreachable | Connection error on the poll | Retry with backoff; other profiles unaffected (ADR-010) | Profile marked unreachable, with the last successful poll time. Its figures are greyed, never zeroed |
| Poll slower than the interval | Poll duration exceeds the interval | Interval stretches; polls do not queue up | A stale indicator on the profile after two missed intervals |
| Management API reachable, credentials rejected | 401 from the management API | Stop polling that profile; do not retry in a loop | Preflight-style error naming the credential, not an empty queue list |
| Management API returns a queue the profile cannot read | Per-queue 403 | Queue listed with metadata unavailable | Queue row states that metadata is not readable, distinct from "no consumers" |
| DLX presence changes after onboarding | Next poll differs from the recorded value | Update `deadLetterConfigured` | The dead-letter tile switches to "unavailable" rather than to `0` |
| Clock skew with the broker | Timestamps in the future or far past | Flag on the profile; `x-death` times marked unreliable | Preflight `clock_skew` failure carried into the dashboard |

**The poller never writes to the broker.** A read path that mutates is how a monitor becomes a suspect during an incident.

## 3. Topology manager

Declares and bounds the shadow queue, then binds it. Runs once per queue, at enablement and after a broker restart that lost non-durable state.

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| `configure` refused | Declare rejected | Do not retry; mark capture unavailable for that queue | `captureState: unavailable` with the missing grant named (`ERR-BRK-001`) |
| Queue exists with different arguments | Declare fails on inequivalent arguments | Do not delete and redeclare — that would drop buffered copies | Error naming the conflicting arguments; the operator decides |
| Binding refused (`write` missing on the exchange) | Bind rejected | Capture unavailable for that exchange, others unaffected | The exchange is named, because "capture is off" without a target is unactionable |
| Declare succeeds, bind fails | Partial setup | Remove the orphan queue it just declared, then report | A clean failure rather than a queue filling with nothing |
| Broker restarts and loses a non-durable queue | Queue absent on the next poll | Re-declare and re-bind automatically | A capture-gap warning covering the gap, not silence |

**The orphan case is the one worth coding carefully.** A declared-but-unbound queue fills with nothing, so it costs disk and looks healthy. Removing it on the failure path is the difference between a clean error and a slow leak.

## 4. Consumer loop

One connection per queue shard, prefetch above the batch size, manual ack.

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| Connection drops | Client shutdown event | Reconnect with backoff; unacked messages are redelivered by the broker | Brief gap, then recovery; the gap is visible as a dip, not as zero |
| Consumer is cancelled by the broker (queue deleted) | Server-initiated cancel | Stop consuming; do not spin | `captureState: unavailable`; the queue is gone, which is the operator's news to hear |
| Delivery rate exceeds the writer | Batch buffer grows | Buffer bounded; on overflow, stop consuming rather than grow memory | `captureState: stale`, because capture has stopped advancing. The profile's error count is what distinguishes a buffer stall from a network stall |
| Broker applies flow control | Publish rate on the adopter's side drops | Nothing: Marites must not be the reason flow control engages (NFR-002) | Not visible to Marites by design; the load test (TC-012) is what catches a regression |
| Ack lost after commit | Redelivery of an already-committed batch | Ledger absorbs it; counts unchanged (ADR-019) | Nothing — which is correct, and is why the ledger exists |

## 5. Batcher and batch writer

`Channel<T>` between the consumer and PostgreSQL; one transaction per batch.

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| PostgreSQL unavailable | Connection error at flush | Retry the batch with backoff; do not ack | `captureState: stale` after the threshold, with depth rising beside it |
| Commit fails on a constraint | Write error | The batch is not acked, so the broker redelivers; log the failing row class | An error count on the profile, not a silent drop |
| Transaction takes longer than the batch interval | Commit duration | Batches stay serialised per queue — one writer per queue by design (ADR-009) | Throughput falls; no correctness change |
| Process killed mid-batch | No commit | Redelivery; ledger deduplicates | Nothing |
| Disk full at the database | Write error | Row writes stop; rollups also stop, because they share the transaction | `captureState: capped` **only** when the cause is the ceiling; a full disk is an infrastructure failure and must read as one |

**The last row is a distinction the UI has to make.** "Capped" means the product chose to stop. "Database full" means the environment failed. Collapsing them hides an infrastructure incident behind a product setting.

## 6. Partition sweeper and compaction

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| Sweeper stops running | No partition dropped in a window longer than retention | Partitions accumulate; storage grows | Storage tile rising with no retention effect; a health check on the sweeper itself |
| Sweeper drops a partition early | Counts change for a period that had rows | Should be impossible: the sweeper cannot see `message_rollup` | A defect, not a state. TC-015 is the assertion |
| Compaction fails partway | Aggregation error | Finer rows are deleted only after the coarser row is committed | The tier count is unchanged; retry is safe because compaction sums |
| Compaction runs twice for a window | Duplicate aggregation | Upsert on `(queue_ref_id, granularity, bucket_start)` with additive increments would double-count | Guard: compaction writes a marker per window, or aggregates from a snapshot. **Not yet specified** — recorded as a gap |
| Ceiling reached | Free space below the ceiling | Row writes pause; rollups continue | `captureState: capped` (DOC-DATA-001 §4.1) |

The double-compaction row is the most serious open item in this document. Additive upserts plus a retried compaction would inflate a total permanently, and totals are never rebuilt from rows. It needs a specified guard before M2.

## 7. Replay executor

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| Publish fails partway | Error after N messages | Job stops; state `failed` with `halt_reason`; already-published messages stay published | `202` with `Unresolved`, not a failure — retrying blindly would double-send (`ERR-OPR-004`) |
| Target queue grows faster than it drains | Depth sampled during the job | Auto-halt; state `halted`; requires a human resume | The reason, the depth trend, and a resume control |
| Process dies mid-job | Job record left `running` | On restart, the job is reconciled against its processed count, not restarted from zero | The job appears as interrupted and is resumed or abandoned explicitly |
| Same `content_hash` replayed repeatedly | Count of consecutive replays | Capped; the operator is told why | A refusal, not a silent throttle |
| Confirmation token expired between preview and execute | Token TTL check | `ERR-OPR-001`; nothing sent | "Confirmation expired" with a re-confirm action |

**A replay job is never deleted.** It is the record of what was re-sent, and the audit row alone does not carry per-message progress.

## 7.1 Destructive operations — the hold

Every discard, purge, replay and publish passes through the same three steps (ADR-023): hold the bodies durably, issue the command, record the outcome. The failures that matter are the ones that land between those steps.

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| The hold cannot be written | Insert fails or times out | **No command is issued.** The messages are untouched | `ERR-OPR-007`: nothing was discarded, the record could not be written |
| The broker rejects the command | Provider error at issue | Intent marked `abandoned`; the audit records the attempt as `failure` with no evidence | The provider's reason, and a record that nothing was destroyed |
| The process dies after the command, before the record | Intent left `held` past the reconciliation window | On restart the intent is resolved against the broker — message present means it was not discarded, absent means it was — and the audit row is written from that answer | The operation appears once, with the outcome the broker reported |
| The record cannot be written after a successful command | Commit fails | The intent survives and the promotion is retried; the payload is already safe from the hold | The operation completes late; nothing is lost |
| A purge cannot be reconciled by message | Queue emptied, so individual messages cannot be asked about | Depth before and after, plus the operation's own count, are the evidence | The count, and a note that per-message confirmation was not possible |

**The direction of every error is deliberate.** The pair is never "destroyed with no record": the hold is committed first. The residual risk is the opposite — a record of a destruction that did not happen — which is detectable, because the message is still there.

## 8. API request path

| Failure | Detection | Behaviour | Operator sees |
|---|---|---|---|
| Database unavailable | Connection error | `503` with `ERR-CMN-00x`; no partial response | A clear unavailability, not an empty list |
| Token validation cannot reach Entra | Key fetch failure | Cached keys serve until expiry; then `401` | Sign-in failure, with the cause stated in the log rather than the body |
| Rate limit exceeded | Token bucket empty | `429` with `Retry-After` (`ERR-CMN-004`) | Wait and retry |
| Capability not supported by the provider | Capability record check | `422` (`ERR-CAP-001`) | The control should not have been offered; the error exists because clients can be wrong |
| Stale revision on a config write | `If-Match` mismatch | `409` (`ERR-CMN-006`); no write | Re-read and re-apply |

## 9. Cross-cutting failures

| Failure | Effect | Mitigation |
|---|---|---|
| Broker clock skew | `x-death` timestamps wrong, silently | Preflight check; timestamps marked unreliable on the profile |
| Network partition between Marites and the broker | Capture stops; queues keep growing | `stale` after the threshold, with depth rising beside it — the one signal that distinguishes this from quiet traffic |
| Adopter deletes a watched queue | Consumer cancelled; metadata 404 | Queue marked unavailable, not removed: the adopter may recreate it with the same name |
| Two workers claim one queue | Rollup upsert serialises; counts still correct but slow | Advisory-lock ownership is a correctness requirement (ADR-009) |
| One cluster stalls the others | Should not happen | Bulkhead isolation per profile (ADR-010); TC-023 asserts it |

## 10. What this document does not cover

- **Quantified thresholds.** "After two missed intervals" is illustrative. The real thresholds are implementation values and are not set.
- **Alerting.** Nothing pages anyone. Every signal here is pull-only, which is a product gap recorded in the [platform foundation index](../platform-foundation/README.md).
- **Broker-side failures.** If the broker loses messages, Marites can only report the divergence it observes.

## 11. Document control

Revision 0.1 records designed failure behaviour for nine components. Written 09 November 2026 to close the gap named in the [architecture index](README.md). One item is flagged as unspecified rather than designed: compaction idempotency (§6). Nothing here has been observed against a running system.
