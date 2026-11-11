---
document_id: DOC-UX-001
title: Dashboard and Operations UX Specification
product: Marites
version: 0.2
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-UX-001 — Dashboard and Operations UX Specification

**Source:** [requirements](../02-business/requirements.md) FR-004…FR-011, FR-017, FR-018. Owning document for metric presentation, role capabilities, and operation safety. Metric definitions are owned by [DOC-DATA-001 §3](../07-data-and-contracts/data-model.md); this document governs how they are displayed.

## 1. Dashboard metrics

Four numbers on the overview, defined once so the UI and API cannot drift:

| Tile | Source | Definition |
|---|---|---|
| **Messages published** | `rollup.published` | Messages routed into a watched exchange, in the window |
| **Dead letters** | `rollup.dead_lettered` | Messages arriving at a DLX, in the window |
| **Dead-letter rate** | derived | `dead_lettered / published`, same window |
| **Peak rate** | `rollup` | Highest messages/second observed in the window |

Four presentation rules, each preventing a specific misreading:

1. **Never show `published + dead_lettered` as a "total messages" figure.** They overlap by construction — a dead letter is a published message seen a second time. The combined number is a pipeline metric and belongs on an internal ops view, never the dashboard.
2. **Never show a dead-letter count without its rate.** "500 dead letters" is alarming at 600 msg/min and unremarkable at 600,000. The rate is the interpretable number; the count is context.
3. **Always break the dead-letter tile down by reason** (values: [VOCABULARY §4](../14-reference/vocabulary.md)). The same count is a different incident depending on whether a consumer is failing or a queue hit its length limit.
4. **Zero must be distinguishable from blind.** A dashboard reading `0` is ambiguous: either nothing is flowing, or capture has stopped. The overview requires a separate capture-health indicator — last message seen per queue, with a stale badge when capture stops advancing while depth grows (FR-018).

### Time range and granularity

Executives ask per day, week, month, quarter and year. Those are served from stored rollup tiers rather than recomputed from raw events:

| Requested period | Served from | Available back to |
|---|---|---|
| Hour | `minute` rows for the last 7 days, `hour` rows beyond that | Rollup retention |
| Day | `day` rows | Rollup retention |
| Week, month, quarter, year | `day` rows, summed at query time | Rollup retention |

Four rules:

1. **Offer only granularities that exist for the chosen range.** The API reports availability per range; the UI hides the rest. A per-minute view of six months ago must never render as an empty chart — that reads as "no traffic" when it means "not retained".
2. **Changing the range may change the granularity.** Selecting "last 12 months" must not leave a per-minute granularity selected and then show one data point.
3. **Every chart states its granularity in the caption** — "daily totals", not just a date axis. A daily total mistaken for an hourly one is a wrong number presented confidently.
4. **An empty period is labelled "no traffic recorded"**, distinct from "capture was not running". Same distinction as rule 4 above, applied to time rather than to queues.

The per-queue table below the tiles carries `published`, `dead_lettered`, dead-letter rate, current backlog, and last-seen.

**Backlog comes from the metadata poller, not the rollup.** One is live broker state, the other historical volume. Mixing them is how dashboards start lying.

## 1.1 Two surfaces

Viewer is an **executive reporting** audience, not an operations one. The product therefore has two surfaces, and they share no layout.

| | Operations console | Reporting dashboard |
|---|---|---|
| Audience | Support, Admin | Viewer |
| Data source | `message_capture` + live broker metadata | `message_rollup` only |
| Shows | Queue tree, routing keys, message list, payloads, actions | Volume, trend, dead-letter rate, per-queue breakdown |
| Never shows | — | Message bodies, payloads, operations |
| Provider screens | Per-provider — exchanges, topics, partitions differ | Provider-agnostic |

The reporting dashboard is the one place a provider-agnostic UI works, because counts are normalised across all three providers: `published` and `dead_lettered` mean the same thing everywhere. Provider-specific screens exist for operations, where the addressing models genuinely differ. That asymmetry is worth preserving — it means three providers cost two bespoke operational screen sets (exchange-based and topic-based), not three.

**The reporting dashboard never queries `message_capture`.** It reads rollups exclusively, so an executive asking for twelve months of history is a small indexed scan regardless of how much traffic was captured. If it ever joins captured rows, the cost of the executive view starts scaling with storage, and the view that should be cheapest becomes the most expensive.

## 2. Roles and capabilities

Roles arrive as Entra App Roles in the `roles` claim and map to **capability policies**, never to endpoints. The mapping lives in exactly one place (`Marites.Application` authorization orchestration; backend DDD §3).

| Capability policy | Viewer | Support | Admin |
|---|---|---|---|
| View aggregate volume, trends, dead-letter rate | ✓ | ✓ | ✓ |
| View queue depths, rates, DLQ badges | ✓ | ✓ | ✓ |
| View message list and message detail | — | ✓ | ✓ |
| View message payloads | — | ✓ | ✓ |
| Peek / backfill a queue | — | ✓ | ✓ |
| Discard a single message | — | ✓ | ✓ |
| Replay (rate-limited, dry-run enforced) | — | ✓ | ✓ |
| Publish arbitrary message | — | ✓ | ✓ |
| Purge a queue | — | — | ✓ |
| Manage broker profiles | — | — | ✓ |
| Manage capture rules | — | — | ✓ |
| Assign roles | — | — | ✓ |
| View audit log | — | ✓ | ✓ |

**Publish is granted to Support, with guardrails.** It is the highest-risk operation in the product: it injects a message into a live business flow, and a malformed one can fail downstream consumers or corrupt state. It is granted because re-sending a corrected message is the last step of the core recovery loop — but it is paired with all of:

- JSON validation against the declared content type before send
- a required idempotency key, so a double-click cannot produce two messages
- explicit target confirmation showing exchange, routing key, and environment
- typed environment tag when the target is `prod`
- an audit row recording the payload hash, actor, and target

Where an adopter's policy forbids Support publishing, `CanPublish` is revoked per tenant. That is configuration, not a code change — which is the reason capabilities are modelled as policies rather than as role checks.

**Viewer payload visibility is settled by DEC-002: none.** Payloads routinely contain PII and tokens, so Viewer has no message list, detail or payload, and `QUERY /messages` plus `GET /messages/*` return 403. There is no redacted variant.

## 3. Operations and their safety model

Every mutating operation passes through one executor with a shared guard chain:

```
authorise (role + tenant) → validate (limits, target exists)
  → dry-run preview (count + sample, returned to UI)
  → confirm token (server-issued, 60 s TTL, stateless HMAC)
  → rate limit (token bucket per tenant)
  → execute → audit row → return result
```

| Operation | Mechanism | Guard |
|---|---|---|
| **Peek / view** | Store lookup, or Management API get+requeue for uncaptured | Store lookup is read-only. Get+requeue is rate limited and the UI warns that it is not side-effect free (DOC-ARCH-001 §2, Strategy B) |
| **Discard** | Ack from shadow queue; or Management API purge (destructive) | Preview count, typed queue-name confirmation |
| **Replay** | Republish to original exchange + routing key, `x-marites-replay-of` header, TTL guard | Dry run first, max N/min, optional target override, auto-halt if the queue grows during replay |
| **Publish** | Publish arbitrary message to an exchange | Editor with JSON validation, required idempotency key |

### Operations vary by provider

Operation controls are rendered from the broker's `ProviderCapabilities` ([DOC-ARCH-001 §6.2](../05-architecture/architecture-overview.md)), never hard-coded. Where `can_replay` is false the control is **omitted**, with the support tier as the stated reason — a disabled button with no explanation is a support ticket.

Replay is labelled by its actual mechanism, because the four models have different blast radii:

| `replay_model` | Control label | Confirmation |
|---|---|---|
| `republish` | "Replay messages" | Dry-run, then typed confirmation on `prod` |
| `redrive` | "Redrive from dead-letter queue" | Dry-run, message count |
| `seek` | "Seek subscription to timestamp" | Timestamp picker, warning that backlog will be redelivered |
| `offset_reset` | "Reset consumer group offset" — **Admin only** | Group must be inactive; states that all messages from that offset will reprocess; no per-message limit applies |

`offset_reset` is the one that needs a different shape of confirmation, not merely different wording. It affects a consumer group rather than a message, so the dry-run cannot enumerate what will be reprocessed — the UI must say so instead of showing a count it cannot honour.

### Two rules that are not negotiable

**Replay must not loop.** A dead letter replayed to its origin will dead-letter again if the consumer still rejects it. The dry run surfaces the death reason so the operator sees this *before* pressing the button. Consecutive replays of the same `content_hash` are capped, and the replay job auto-halts if the target queue's depth climbs faster than it drains.

**Never auto-purge.** No scheduled destructive operation, ever. Purge is always an explicit human action with a typed confirmation.

### Cluster and environment disclosure

Every operation confirmation displays the cluster name and environment tag. Production is visually distinct. Replay against `prod` requires typing the environment tag, matching the purge pattern — because a Support operator replaying 200 messages while believing they are in staging is more common than anyone admits.

## 4. Empty and degraded states

| State | Presentation |
|---|---|
| No broker configured | Setup wizard, not an empty dashboard |
| Preflight failed on topology permissions | Read-only metadata mode, with the failing check named |
| Queue has no DLX | Warning on the queue, explaining that dead-letter monitoring requires adopter configuration |
| Capture stopped while depth grows | Stale badge with last-seen timestamp — never a bare zero |
| Window has no data | Explicit "no messages in this window", distinct from "capture unhealthy" |

The distinction in the last two rows is the point of this section. A monitoring product whose blank state is ambiguous has failed at its only job.

## 5. Capture policy configuration (Admin)

Per queue, Admin controls what is stored:

| Field | Default | Effect |
|---|---|---|
| `payload_policy` | `sampled` (1 in 100) | Whether payloads are stored at all |
| `row_retention_hours` | 72 | How long messages stay inspectable |
| `max_rows` / `max_bytes` | unset | Per-queue caps beneath the global ceiling |

Defaults are chosen to work without configuration: `sampled` means an operator inspecting a failure usually finds a payload, at roughly 2% of the storage cost of `full`. `none` would ship a feature that always returns empty; `full` would ship unbounded cost.

**Non-default values are marked.** Any field the Admin has changed from its default carries a visible indicator, so an expensive configuration is never mistaken for the shipped behaviour.

**Projected cost before saving.** Changing any of these shows the projected impact at the *observed* rate, not a configured guess — *"full payloads on `orders` at 4,200 msg/s ≈ 360 GB/day; 72 h retention implies ~1 TB"*. Using the rollup rate means the projection reflects reality rather than intention, which is the only version an admin can act on.

**Two warnings:**

- **Approaching the ceiling** — a banner on the broker with the queue list sorted by storage, so the admin can see which queue to change first.
- **At the ceiling** — capture is marked **"capped"**: rows stop, counts continue ([DOC-DATA-001 §4.1](../07-data-and-contracts/data-model.md)). The label must never read as zero traffic.

**What Admin cannot change, and must be shown why.** Partition interval and the global storage ceiling are operator settings, rendered read-only with the reason stated. A control that appears interactive but silently does nothing is worse than an absent one.

**No masking, by design.** Marites stores payloads exactly as published (DEC-006). The capture policy screen states this plainly rather than offering a redaction control that could not work reliably against an arbitrary message schema. Adopters who must not retain a field remove it upstream, or set `payload_policy = none` — counting continues either way.

## 6. Document control

Revision 0.2 records the API method alignment for message search and detail while preserving the established Viewer access rule. DEC-002 governs §2 and the payload endpoint. Wireframes are a separate record ([wireframes and navigation](wireframes-and-navigation.md)) and follow DEC-002 and the contract.
