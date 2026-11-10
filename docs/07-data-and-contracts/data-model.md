---
document_id: DOC-DATA-001
title: Data Model Specification
product: Marites
version: 0.1
date: 2026-11
status: Draft — all decisions resolved; no implementation
---

# DOC-DATA-001 — Data Model Specification

**Source:** [requirements](../02-business/requirements.md), [architecture](../05-architecture/architecture-overview.md). Owning document for table structures, counting semantics, and retention. Enumerated values are cited from [VOCABULARY](../14-reference/vocabulary.md).

## 1. Principles

- **Tenant-scoped from day one.** A tenant is a team inside one organisation (ADR-014), not a customer of a hosted service. Retrofitting tenancy is a rewrite.
- **One writer per table.** `message_capture` is written only by capture workers; `operation_audit` only by the operations executor; configuration tables only by the API. This is what allows the API to stay stateless and scale horizontally.
- **PostgreSQL only** (ADR-004). Binary `COPY`, native partitioning, `JSONB`, and GIN are all Postgres-specific and all load-bearing.

## 2. Tables

Twelve tables in **two stores** ([DEC-012](../01-project/decisions-and-open-questions.md)). Nine are Marites' own bookkeeping and always exist; three hold the traffic and exist only when the adopter connects a message store. The asymmetry is the whole design: one table is enormous, eleven are not, and only the enormous one belongs to the adopter.

### 2.1 Marites' own store — always present

Embedded SQLite, one file, created at install. Nothing here is asked of the adopter, and nothing here grows with traffic.

```sql
tenant            (id, name, slug, created_at)

broker_profile    (id, tenant_id, name, provider_kind, environment,
                   endpoint_enc, admin_uri, credential_kind, credentials_enc,
                   credentials_updated_at, namespace_or_vhost, tls_opts,
                   capabilities_json, is_default, preflight_state, preflight_at,
                   revision)

queue_ref         (id, broker_profile_id, name, source_kind, vhost, is_dlq,
                   dead_letter_source, bound_at, capture_enabled)

capture_rule      (id, queue_ref_id, bind_pattern, enabled, capture_mode,
                   payload_policy, sample_rate, row_retention_hours,
                   max_rows, max_bytes, revision)

replay_job        (id, tenant_id, queue_ref_id, state, total, processed,
                   failed, halt_reason, created_by, created_at, updated_at)

operation_audit   (id, tenant_id, actor_id, actor_kind, on_behalf_of, proposal_id,
                   action, target, params_json, result, message_count,
                   payload_hash, payload_edited, via, created_at, ip)

app_user          (id, tenant_id, entra_object_id, email, role,
                   is_break_glass)   -- role values: VOCABULARY §2

operation_payload (id, audit_id, message_id, content_hash,
                   payload_before, payload_after, payload_size,
                   retained_until, created_at)
                   -- DEC-018: one row per message an operation touched; swept on the capture clock

operation_intent  (id, tenant_id, actor_id, actor_kind, on_behalf_of, proposal_id,
                   action, target, params_json, bodies_json, state,
                   created_at, expires_at)
                   -- ADR-023: the durable hold. Written and committed BEFORE the broker
                   -- command; promoted to operation_audit + operation_payload on success,
                   -- marked abandoned if the command did not take effect. Swept in minutes.
```

### 2.2 The message store — optional, and the adopter's

PostgreSQL (recommended) or SQL Server. Created and granted by the adopter ([DEC-012](../01-project/decisions-and-open-questions.md), [storage prerequisites](../11-operations/storage-prerequisites.md)). Without it the product runs **live-only**: no counts over time, no message list, no search — and full recovery, because the audit is in §2.1 ([DEC-016](../01-project/decisions-and-open-questions.md)).

```sql
message_rollup    (queue_ref_id, granularity, bucket_start, published,
                   dead_lettered, bytes, peak_rate, death_reasons_json)
                   -- PK (queue_ref_id, granularity, bucket_start)

message_capture   (id, tenant_id, broker_profile_id, queue_ref_id,
                   exchange, routing_key, message_id, correlation_id,
                   headers_json, payload_inline, payload_ref, payload_size,
                   content_type, content_hash, captured_at, is_dead_letter,
                   death_reason, native_reason, death_count, first_dead_at,
                   origin_source, origin_capture_id, replay_of_id)

capture_dedupe    (queue_ref_id, identity, remaining, committed_at)
                   -- ADR-019: the redelivery ledger. It lives here, with the messages,
                   -- because its write must share the capture batch's transaction —
                   -- splitting the stores would reopen the double-count ADR-019 closes.
```

Column-level definitions — type, nullability, default, constraint and the reason the column exists — are owned by the [data dictionary](data-dictionary.md). This block is the logical shape only: names, and the table each lives in.

**Multi-provider columns.** `broker_profile.provider_kind` selects the adapter; `capabilities_json` caches the provider's declared capabilities (DOC-ARCH-001 §6.2) so the UI can gate features without a live round-trip. `queue_ref.source_kind` distinguishes a queue, an exchange, a topic, or a subscription — the table name is historical and predates multi-provider scope; renaming it would touch every specification for no functional gain, so the meaning is fixed here instead. `dead_letter_source` replaces the RabbitMQ-specific `dlx_name`, because not every provider has a dead-letter *exchange*.

**Payload evidence is a snapshot, not a column.** `operation_payload` exists because discard and purge delete the row a reference would point at, so the body has to be copied *before* the deletion, in the same transaction. It is deliberately not a column on `operation_audit`: that table is append-only and never swept, and a payload there would outlive every retention promise in the product (DEC-017).

**One row per message, not a sample.** A purge of 1,204 messages writes 1,204 evidence rows (DEC-018). A sample answers *"what kind of message"* but not *"was this message in there?"*, and the second question is the one a dispute asks. Replay writes `before` and `after`; the other three write `before` only.

**Whether a body was edited is recorded on the audit row, not derived from the evidence.** `operation_audit.payload_edited` is set at write time when any evidence row carries a `payload_after` that differs from its `payload_before`. It could be computed from `operation_payload`, but that table is swept on the capture clock, and *"this replay was not a plain resend"* is a fact about the operation that has to outlive the bodies it describes.

**A proposal is an audit row, not a table.** `action = proposal` with `result = pending` records that an agent asked for something; the operation that follows carries `proposal_id`. Two rows, one decision, no eleventh table — and the proposal survives the approval, which is the point (DEC-010).

**Two stores, two engines, one rule.** A table lives in the message store if and only if it grows with traffic. Everything else is bookkeeping and lives with Marites. The one deliberate exception is `capture_dedupe`: it is bookkeeping by nature but sits with the messages, because ADR-019 requires its write to be in the same transaction as the batch insert, and a transaction cannot span two databases.

**The hold is a table, not memory.** `operation_intent` exists because a destructive operation cannot be recorded after it succeeds: for discard and purge the bodies stop existing the moment the broker confirms, so the only copy available afterwards is one taken before. The intent row is written and committed **before** the command is issued, promoted into `operation_audit` + `operation_payload` when the command succeeds, and marked `abandoned` when it did not. It is the write-ahead log for operations, and it is swept in minutes rather than days (ADR-023).

**Death fields are normalised, not flattened.** `death_reason` is the cross-provider enum from VOCABULARY §4; `native_reason` preserves the provider's own string verbatim. Normalisation that discards the original wording is how a diagnostic tool stops being diagnostic.

`message_capture` is partitioned by day on `captured_at`. After three months that is roughly ninety physical partitions of **one** logical table — worth stating because it makes `\dt` look alarming.

### Column notes that matter

| Column | Why it exists |
|---|---|
| `credential_kind` / `credentials_enc` | `credential_kind` selects the payload shape (VOCABULARY §13); `credentials_enc` holds that payload as JSON, encrypted with a per-row data key under a KEK from env/KMS. RabbitMQ populates two slots (`amqp`, `admin`). Ambient kinds store no secret. Never logged, never returned by any endpoint (NFR-003). |
| `preflight_state` / `preflight_at` | Last readiness result. A profile cannot be enabled for capture until preflight passes; re-running is a button, not a migration. |
| `payload_inline` / `payload_ref` | Inline `bytea` up to 64 KB; larger payloads go to an object store with only a key in the row. Prevents a 50 MB message from wrecking a table scan. Two columns rather than one, because the row owns the inline body while the sweeper must also delete the spilled object — see [data dictionary](data-dictionary.md) §6. |
| `content_hash` | **HMAC-SHA256** of payload plus key headers, keyed per tenant. Enables duplicate grouping — "this same message appeared 400 times" — which is the most useful view when a DLQ is flooding. HMAC rather than bare SHA-256 deliberately: a plain hash of a sensitive value is a confirmation oracle, letting someone test whether a suspected card number is present without decrypting anything. Keyed hashing preserves grouping and removes that capability. |
| `is_dead_letter` / `death_reason` | Parsed from `x-death` at ingest. Turns "a pile of messages" into "1,204 rejected, 3 expired, 88 dropped for maxlen". |
| `origin_capture_id` | Links a dead letter to the publish that produced it. Best-effort, nullable; counts never depend on it (§3.2). |
| `replay_of_id` | Replay never deletes the original record; it links. Provenance stays intact. |

### Deliberately not tables

- **Confirm tokens** — stateless HMAC-signed values with a 60-second expiry. A table would add a write per operation plus a cleanup sweeper for no benefit.

  The signed payload is `{actor_id, target, action, params_hash, issued_at}`, and every field is checked on use:

  | Bound to | Why it is in the signature |
  |---|---|
  | `actor_id` | A token issued to one person cannot be replayed by another, including a shared service principal |
  | `target` | The queue the operator read the confirmation from is the queue that is acted on — a token for `orders` is refused on `payments` |
  | `action` | A `discard` token cannot execute a `purge` against the same target |
  | `params_hash` | The parameters cannot change between preview and execute; a token is bound to the operation the operator saw |
  | `issued_at` | 60-second expiry, so a stale confirmation cannot be applied to a queue whose contents have moved |

  A mismatch on any field returns `ERR-OPR-001` and sends nothing. The token is not a session credential and grants no access on its own.
- **Sessions and refresh tokens** — Entra ID owns them. Storing them locally is a liability.
- **Payloads over 64 KB** — object store with a row reference.
- **Schema version** — EF Core migrations history.

## 3. Volume accounting — counting is not storing

### 3.1 Two tiers

The requirement is exact counts of dead-lettered versus healthy traffic, per queue, over time. That is a counting problem. "Capture everything" is usually read as "write a row for every message", which at 5,000 msg/s is 432 million rows per day — roughly 100 GB/day of metadata before any payload (NFR-001). No retention window fixes that.

| Layer | Holds | Coverage | Retention |
|---|---|---|---|
| `message_rollup` | Per-queue, per-minute counters: published, dead-lettered, bytes, breakdown by reason. Written additively by capture, rolled up by **recomputation** so a retried compaction is harmless (ADR-024) | **100%, exact** | Months |
| `message_capture` | One row per message: headers, routing key, size, `x-death`, payload per policy | Bounded window | Hours–days |

The rollup is written from the same in-memory batch the capture worker already builds — counters incremented per message, flushed once per second, in the same transaction as the row `COPY`. It costs a few hundred bytes per queue per minute; a hundred queues for a year is single-digit gigabytes. **Exact counts, forever, for almost nothing** (NFR-007). *Exact* means each delivered copy is counted once. A crash between `COMMIT` and `ack` redelivers a batch, and a bounded shadow queue can drop copies; ADR-019 handles both: a ledger absorbs redelivered duplicates, and the capture-gap warning (FR-018) flags dropped copies.

**`capture_mode` decides whether a row exists at all.** `rows` writes one per message; `counts` writes none and increments the rollup only (DEC-013, VOCABULARY §16). It is a different question from `payload_policy`: that one decides whether the body is kept inside a row. A queue in `counts` mode still reports exact `published` and `dead_lettered`, and its row retention setting is inert.

`payload_policy` — `full`, `sampled`, `none` — governs **payload retention only**. Counting is never affected by payload policy. A queue set to `none` still reports perfect counts.

The rollup is **never** backfilled from `message_capture`, because rows expire and counts must not. If rollups were derived from rows, every retention sweep would silently rewrite history.

### 3.1.1 The retention invariant

> **Admin-set retention applies to `message_capture` only. It never changes a count.**

Shortening row retention from 90 days to 7 days changes what an operator can *inspect*. It must not change what an executive can *count*. The row sweeper has no access to `message_rollup`, and that separation is a correctness requirement, not a preference.

Two guards enforce it:

- **Rollup retention must be ≥ row retention.** If rows outlive counts, the dashboard shows zero for a period the operator can still browse — the most confusing possible state.
- **Rollup retention must be ≥ the reporting horizon.** If executives ask for year-over-year, a two-year rollup window silently truncates the answer.

### 3.1.2 Rollup tiers

One row per queue per minute, forever, is 1,440 rows per queue per day — roughly 158 million rows for a hundred queues over three years. Survivable, but wasteful for a question a daily total answers.

| Tier | `granularity` | Retained | Serves |
|---|---|---|---|
| 1 | `minute` | 7 days | Live view, incident forensics |
| 2 | `hour` | 90 days | Recent trend |
| 3 | `day` | Rollup retention (default 3 years) | Week, month, quarter, year |

A compaction job aggregates tier 1 into tier 2 and tier 2 into tier 3, then deletes finer rows past their window. Compaction **sums** counts and takes the **maximum** of `peak_rate`; it never samples. Totals are therefore identical at every tier — a day row equals the sum of the 24 hour rows it replaced.

**Week, month, quarter and year are never stored.** They are summed from day rows at query time. A year is 365 rows per queue, so the cost is trivial, and storing derived periods would create a second place for totals to drift.

**Granularity availability depends on age.** A per-minute view of six months ago does not exist. The API reports which granularities are available for a requested range, and the UI must not offer the others — an empty chart that looks like "no traffic" is exactly the failure this product exists to prevent.

### 3.2 The double-counting rule

A dead letter is the **same message observed twice**: once entering its original exchange, once arriving at the DLX. If both are captured, a naive total counts it twice.

These are therefore separate series and must never be summed:

| Metric | Definition |
|---|---|
| `published` | Messages routed into a watched exchange |
| `dead_lettered` | Messages arriving at a DLX, split by `death_reason` |
| `dead_letter_rate` | `dead_lettered / published`, same window |

`messages_seen` is a **pipeline health metric**, not a business metric. It belongs on an internal ops view, never on the dashboard where someone will add it to something.

Rows are linked rather than deduplicated: the dead-letter row carries `origin_capture_id`, matched by `message_id`, falling back to `content_hash` plus routing key within a time window. Best-effort and nullable — provenance is a bonus, never a correctness dependency for counts.

### 3.3 Capturing non-dead-letter traffic

Counting everything requires shadow bindings on **exchanges**, not only on DLXs. For a topic exchange, a single `#` binding captures 100% of routed messages. That is simple and complete, and also the highest-volume option — which is why the rollup tier exists and why `payload_policy` defaults to `sampled` (1 in 100) rather than `full` on firehose exchanges.

## 4. Retention and lifecycle

| Concern | Choice |
|---|---|
| Row retention | Native `RANGE` partitioning on `captured_at`, one partition per day. **Admin-set** per capture rule; default 72 hours |
| Partition lifecycle | Sweeper creates partitions 7 days ahead, drops those past the admin-set row retention |
| Size cap | `max_rows` / `max_bytes` per capture rule, enforced by the same sweeper |
| Rollup retention | **Separate admin setting**, default 3 years, minimum 1 year, floored at or above row retention. Never touched by the row sweeper |
| Rollup compaction | Minute → hour after 7 days; hour → day after 90 days. Sums only, never samples |
| Storage ceiling | Operator setting, **defaulting to `min(50 GB, 20% of available disk)`** — never unlimited |
| Audit retention | Append-only; never deleted by an automated process |
| Payload evidence retention | `operation_payload.retained_until` is set at write to the queue's row retention, and the same sweeper deletes it. An Admin may extend it to at most 30 days; the UI states that this outlives the rows |
| Redelivery ledger | Written in the same transaction as the batch. `capture_dedupe` rows older than the redelivery window (default 1 hour) are swept with the partitions; sweeping it never changes a count |

The two retention settings are deliberately separate controls in the admin UI, not one number. Collapsing them is how a routine "reduce disk usage" change silently deletes a year of executive reporting.

### 4.1 Storage ceiling and degradation order

Admin-configurable storage is bounded by an operator-set ceiling (`MARITES_STORAGE_CEILING_BYTES`), because the person choosing a payload policy often does not know the volume implications. When the ceiling is reached:

1. **Row capture pauses.** `message_capture` writes stop; the disk does not fill.
2. **Rollup writes continue.** `message_rollup` is small enough to keep writing, so `published` and `dead_lettered` keep incrementing.
3. **The UI shows capture as capped** — never as zero traffic.

The degradation order is deliberate: **investigation is sacrificed before reporting.** A monitoring product that stops counting because a disk filled has failed at the one thing it was bought for. Losing the ability to inspect a payload is an inconvenience; losing the counts is a product failure.

Recovery is automatic: once the sweeper frees space below the ceiling, row capture resumes without operator action.

Retention is enforced per capture rule by the background sweeper — day-partition drop plus size cap — never by ad-hoc `DELETE`. A `DELETE` sweep over a partitioned table is the difference between dropping a file and rewriting millions of rows.

## 5. Access strategy

| Concern | Choice |
|---|---|
| CRUD (brokers, rules, users) | EF Core 9 + Npgsql |
| Message capture | Raw binary `COPY`, batched 500 rows or 1 s |
| Header queries | `headers_json` as `JSONB` + GIN index |
| Payload | Inline `bytea` up to 64 KB (TOAST handles it); larger spills to object store |
| Text search | `pg_trgm` on `routing_key` / `message_id` at M3; full-text on payload only if demanded |
| Rollup upsert | `ON CONFLICT DO UPDATE` with additive increments, keyed `(queue_ref_id, granularity, bucket_start)`; `peak_rate` merges with `GREATEST` |

The rollup upsert is the one place where two writers on the same key would serialise. Queue-shard ownership (DOC-ARCH-001 §9) guarantees exactly one worker per queue, so contention is negligible — and that guarantee is a correctness requirement, not an optimisation.

## 6. Document control

Revision 0.1 records the initial model. DEC-005 set the `sampled` default and DEC-006 settled verbatim storage; both are answered. `peak_rate` was added to `message_rollup` in November 2026 because the dashboard tile and the contract's `peakRate` cannot be derived from summed rows. ADR-019 added a tenth table, `capture_dedupe`. No migration exists and no schema has been applied.

*Amended November 2026:* four columns added that the contract already required and the model did not carry — `broker_profile.revision` and `credentials_updated_at`, `capture_rule.revision` and `sample_rate`. Without them `If-Match` had nothing to match and the `sampled` policy had no rate to sample at. `payload_ref` split into `payload_inline` and `payload_ref`, because one column cannot hold both an inline body and an object key without a discriminator that already means something else. The [data dictionary](data-dictionary.md) now owns the column-level definition.
