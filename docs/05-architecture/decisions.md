---
document_id: ADR-REGISTER
title: Architecture Decision Register
product: Marites
version: 0.1
date: 2026-11-07
status: Draft — 22 accepted, 1 superseded, none proposed
---

# Architecture Decision Register

**Single controlled register for all Marites architecture decisions.** Specification documents cite these identifiers; they do not maintain their own decision tables.

**Numbering rule:** identifiers are assigned once and never reused. A withdrawn decision is marked `Withdrawn`, not deleted. A superseded decision is marked `Superseded by ADR-###`.

## Status definitions

| Status | Meaning |
|---|---|
| Proposed | Recorded, not yet approved — usually blocked on a DEC in [DECISION-LOG](../01-project/decisions-and-open-questions.md) |
| Accepted | Binding on all implementations |
| Business constraint | Imposed by the adopter or the platform, not a technical preference |
| Superseded | Replaced by a later decision |
| Withdrawn | No longer applicable; identifier retained |

---

## A. Product boundary

| ADR | Decision | Status |
|---|---|---|
| ADR-001 | Marites is a client of an adopter-supplied broker; it never provisions, hosts, upgrades, or repairs RabbitMQ | Accepted |
| ADR-014 | Marites is self-hosted software, not a hosted SaaS; multi-tenancy means one install serving several teams in one organisation | Accepted |

**ADR-001 — rationale.** Every alternative (bundling a broker, provisioning one, offering a managed tap) turns a monitoring product into an infrastructure product with an on-call rotation. The adopter already runs a broker; the product's only job is to observe it. **Consequence:** DOC-OPS-001 §1 becomes a hard prerequisite, and a misconfigured broker produces an application that looks broken — which is why preflight (FR-013) exists rather than being optional polish.

---

## B. Capture architecture

| ADR | Decision | Status |
|---|---|---|
| ADR-002 | Capture uses shadow bindings — a Marites-owned queue bound to the adopter's exchange — as the primary mechanism | Accepted |
| ADR-003 | No second RabbitMQ cluster; where a hop is unavoidable, add a single-process relay, never a broker | Accepted |
| ADR-010 | Multiple clusters are a star of independent connections with bulkhead isolation, never a chain | Accepted |

**ADR-002 — rationale.** AMQP has no peek: consuming is destructive and two consumers on a queue compete rather than both observing. A second binding on the same exchange is the only non-destructive way to receive a copy. **Consequence:** requires `configure`/`write`/`read` on `^marites\.`, which DEC-003 confirmed is granted. Where a managed broker forbids it, the product degrades to backfill plus firehose tracing and loses continuous capture — a condition preflight must detect and name, not conceal.

**ADR-003 — rationale.** A three-hop chain (main → tap cluster → API → database) has three places to fail silently. A monitor that quietly stops monitoring is worse than no monitor, because people trust it. Federation makes the chain possible, not wise. **Consequence:** relays are permitted for network isolation and outage buffering, and any hop must expose lag and last-seen timestamps (FR-018).

**ADR-010 — rationale.** Per-cluster workers and connections prevent one unreachable cluster from stalling the others. **Consequence:** every key is `(broker_profile_id, vhost, queue_name)`; queue name alone is never an identity. Cross-cluster dedupe is out of scope for v1 (DEC-008): it can only operate within the row retention window, so a "unique" figure would change meaning by time range. The primary count is observed, per cluster, and means the same thing at every range.

---

## C. Data

| ADR | Decision | Status |
|---|---|---|
| ADR-004 | PostgreSQL is the only supported store; SQLite is deliberately excluded | **Superseded by ADR-022** |
| ADR-022 | Two stores, not one: Marites keeps its own bookkeeping store (embedded SQLite, always present) and the adopter may connect a message store (PostgreSQL first, SQL Server second) that holds traffic only — both behind one storage port | Accepted |
| ADR-007 | Counting and row storage are separate tiers: `message_rollup` for exact long-range counts, `message_capture` for a bounded investigation window | Accepted |
| ADR-011 | Message capture writes via Npgsql binary `COPY`, not EF Core | Accepted |
| ADR-012 | `message_capture` is range-partitioned by day on `captured_at` | Accepted |
| ADR-019 | Capture counting is redelivery-safe: redelivered messages are checked against a short-lived ledger before they enter rollups, and a gap between observed and broker-reported publish counts is surfaced, never absorbed | Accepted |
| ADR-023 | Destructive operations are staged, not transactional: the bodies are committed to `operation_intent` before the broker command, and the audit row is written only after the broker confirms | Accepted |

**ADR-004 — rationale.** The data layer depends on binary `COPY`, native partitioning, `JSONB`, and GIN indexes — all Postgres-specific and all load-bearing. A second provider means two code paths for the most performance-critical component, and the "lightweight" profile would silently lose partitioning. **Consequence:** local installs run PostgreSQL in a container rather than a file database.

*Superseded 10 November 2026 by ADR-022, at the adopter's request: enterprises will not let a self-hosted product install its own database. The reasoning above is not wrong — it is the reason ADR-022 supports **two** engines rather than "any", and the reason the second one is a milestone of its own.*

**ADR-022 — rationale.** The requirement is "bring your own database", which is how a self-hosted product survives a DBA review — but the first reading of that requirement was too wide: it made the adopter host Marites' own bookkeeping as well as their traffic. **Amended 10 Nov 2026:** there are two stores. Marites keeps configuration, roles, audit, operation evidence and replay progress in its own embedded store, created at install, on SQLite; the adopter may connect a database that holds **only** `message_capture`, `message_rollup` and `capture_dedupe`. The split follows one rule — a table lives in the message store if and only if it grows with traffic — with `capture_dedupe` as the deliberate exception, because ADR-019 requires its write to share the capture transaction. ADR-004's rejection of SQLite stands and does not apply here: it was about binary `COPY` and partition drops at 5,000 msg/s, which no bookkeeping table does. The consequence is that recovery no longer depends on the adopter connecting anything (DEC-016), and that the storage port's four members serve the message store while the bookkeeping store needs nothing more than a transactional file. The risk in ADR-004 was real: four features carry the capture path and each is engine-specific. The answer is not to promise every engine but to make the seam explicit — a **storage port** with four members (bulk write, rollup upsert, partition lifecycle, query), one implementation per engine, and a conformance suite that defines "supported". **Consequence:** PostgreSQL is built first because it defines what the port needs; SQL Server follows as its own increment (M7); a third engine is a milestone, never a configuration. The adopter creates the database or grants `CREATE DATABASE`; Marites always creates the schema. **Two credentials follow:** a migration role that holds DDL and is used briefly, and a runtime role that holds DML only — which is also why migrations are a separate step rather than a startup task (DEC-014).

**ADR-007 — rationale.** "Capture everything" is normally read as "store a row per message". At 5,000 msg/s that is 432 million rows per day. Exact counts are required; per-message rows at full retention are not survivable. Separating them yields exact counts for almost nothing. **Consequence:** rollups are never backfilled from rows — if they were, every retention sweep would rewrite history.

**ADR-011 — rationale.** EF Core's batched insert tops out in the low thousands per second with change tracking; `BeginBinaryImport` exceeds 50k. **Consequence:** the capture path bypasses the ORM, so its schema mapping is maintained by hand and must be covered by integration tests against a real database.

**ADR-019 — rationale.** Ack-after-commit turns a crash between `COMMIT` and `ack` into redelivery. Rows tolerate a duplicate; additive counters do not, and ADR-007 forbids rebuilding them from rows, so one uncorrected duplicate is a permanent over-count. A bounded shadow queue can also drop copies silently. **Mechanism:** for each committed batch the writer records the identities of its messages (`message_id`, else the HMAC `content_hash` plus routing key) in a ledger kept for at least one redelivery window. **The ledger write is in the same transaction as the batch `COPY` and the rollup upsert**, so a crash either persists all three or none; a separate commit would reopen the double-count this decision exists to close. A message the adapter flags as redelivered consumes one ledger entry if present and is skipped; a multiset rather than a set, so genuinely identical messages are still each counted. Separately, observed counts are compared with the broker's publish counter and a divergence raises the FR-018 warning. **Redelivery indicator.** `redelivered` is an AMQP 0-9-1 flag. Each adapter normalises its provider's signal into one `Redelivered` field on the tap envelope: RabbitMQ `redelivered`, Service Bus `deliveryCount > 1`, Pub/Sub `deliveryAttempt > 1` (populated only when the subscription has a dead-letter policy). A provider or configuration that cannot supply the signal declares it in the capability record and counting for that profile is **at-least-once**, not exact; the UI and FR-002, FR-004 and NFR-007 carry that qualifier for it. **Consequence:** one extra table (`capture_dedupe`) and one ledger read per redelivered message. **Decision (07 Nov 2026): Accepted** with the mechanism above, so the counting claim in FR-002, FR-004, NFR-007 and DOC-DATA-001 §3.1 is "exact" wherever the indicator exists. *Amended 07 Nov 2026, same day, after review: transaction scope and the cross-provider indicator were added; the decision itself is unchanged.* If the ledger proves too costly, a superseding ADR must downgrade that claim to "at-least-once".

**ADR-023 — rationale.** The instinct is that "hold the message, run the command, save the record" should be **atomic**. It cannot be, and saying so is the first half of the decision: the broker is RabbitMQ, Service Bus or Pub/Sub, the store is a database, and no two-phase commit exists between them. There is no transaction that spans `basic.ack` and `COMMIT`. Any design claiming otherwise is claiming a guarantee it does not have.

What *is* achievable is a pair of invariants, each enforceable on its own side:

1. **No command is issued without a durable copy of what it will destroy.** For discard and purge this is not a preference — after the broker confirms, the bodies no longer exist anywhere to be recorded. The copy must be taken and committed first, or there is nothing to record.
2. **A command that succeeded is never left unrecorded.** The audit row is written only after the broker confirms, so the log never claims an operation that did not happen.

**Mechanism.** The bodies and parameters are committed to `operation_intent` first. The broker command is issued. On confirmation, the intent is promoted — `operation_audit` and `operation_payload` written in one transaction — and the intent is deleted. If the command fails, the intent is marked `abandoned`, the messages are untouched, and nothing enters the audit. **The window between the command and the promotion is the one place the pair is not atomic**, and it is closed by reconciliation rather than by a transaction: on restart, an intent that is neither promoted nor abandoned is resolved against the broker — a message that is still present was never discarded, a message that is gone was — and the audit row is written accordingly.

**Why not simply write the audit row first with `result = pending`.** Because the audit is append-only, a `pending` row can never become a success — it can only be joined by a second row. Two rows for one operation in a log that humans read is a worse cost than one staging table that they never see.

**Consequence.** A twelfth table, swept in minutes rather than days; a reconciliation pass on the operations executor; `ERR-OPR-007` when the hold cannot be written, because then the command is not issued at all. Purge is the weakest case: once a queue is emptied, "was this message in there?" cannot be asked of the broker, so reconciliation falls back to depth and the operation's own count. **Decision (10 Nov 2026): Accepted.**

---

## D. Code structure

| ADR | Decision | Status |
|---|---|---|
| ADR-005 | Vertical slices organised by capability, with dependency inversion at exactly three ports (`IBrokerAdmin`, `IMessageTap`/`IMessagePublisher`, `IClock`) — not a four-project onion | Accepted |
| ADR-006 | `07-data-and-contracts/openapi/openapi.yaml` is the product boundary; all implementations conform and the frontend client is generated from it | Accepted |
| ADR-009 | Modular monolith with one image and three entrypoints (`api`, `worker`, `all`) | Accepted |
| ADR-013 | No third-party mediator; a ~50-line in-house dispatcher (`IRequest`, `IHandler`, registry, `Send<T>`) | Accepted |

**ADR-005 — rationale.** Only one infrastructure here is genuinely swappable — the broker. PostgreSQL is a commitment, and the HTTP boundary is protected by the contract rather than by interfaces, because the other implementations are in different languages. Enforced by a single ArchUnitNET test rather than by folder ceremony. **Consequence:** `Marites.Api` may not reference `RabbitMQ.Client` or `Npgsql`, and `Marites.Contracts` references nothing.

**ADR-006 — rationale.** The frontend and backend are separate codebases; without an executable contract they drift, and the drift appears at runtime rather than at compile time. Writing the contract first also makes the API reviewable as a design artefact before any code exists. **Consequence:** the OpenAPI spec is written and reviewed before the first C# endpoint, the React client is generated from it, and a black-box conformance suite defines "done" for the service.

**ADR-009 — rationale.** Splitting API and worker is a deployment concern, not a code concern; the same assemblies run either way. **Consequence:** queue-shard ownership via PostgreSQL advisory locks is a correctness requirement, not an optimisation — two workers on one queue would serialise the rollup upsert.

**ADR-013 — rationale.** A mediator was first rejected as unnecessary indirection. Revisited 07 Nov 2026 when handler dispatch ergonomics were requested: the ergonomics are wanted, but not at the cost of a reciprocal licence.

MediatR is dual-licensed under **RPL-1.5** (a strong copyleft licence obliging source disclosure) or a **commercial agreement**. For software distributed to self-hosting adopters that is unacceptable twice over: a closed-source product would have to publish its source, and a commercial licence is per organisation, so the obligation would land on every customer rather than on us.

**Decision:** a ~50-line in-house dispatcher — `IRequest<TResponse>`, `IHandler<TRequest, TResponse>`, a handler registry, and `Send<T>`.

**Consequence:** the dispatcher is ours, permissively licensed, and small enough to reimplement in an afternoon if it ever needs replacing. It must stay that small — the moment it grows pipeline behaviours and conventions, it has become the framework we rejected. Cross-cutting concerns remain explicit decorators in the composition root, visible at the call site.

*Alternatives considered, 07 Nov 2026:*

| Option | Licence | Why rejected |
|---|---|---|
| **MediatR** | Dual: RPL-1.5 (strong copyleft) or commercial | Reciprocal obligation is incompatible with distributing self-hosted software to third parties |
| **Wolverine** | MIT | Solves a problem we do not have. Its value is transports, durable outbox and sagas; our writes are already transactional in one commit, and we have no sagas. Its transport layer would own broker access, conflicting with ADR-015 |
| **No mediator at all** | — | Workable, but dispatch ergonomics were explicitly requested; a 50-line dispatcher delivers them without the licence or the framework |

The decisive constraint is not licence cost but **distribution model and code ownership**: a dependency must not impose obligations on the people who deploy this software, and must not shape the codebase in ways that make it harder to reason about, audit, or replace.

---

## E. Identity

| ADR | Decision | Status |
|---|---|---|
| ADR-008 | Entra ID via OIDC with App Roles mapped to capability policies; a break-glass local admin is required for self-hosted installs | Accepted |

**ADR-008 — rationale.** App Roles arrive in the `roles` claim without a Graph round-trip per request, unlike security groups. Capability policies rather than endpoint checks keep the role matrix in one file. **Consequence:** `MARITES_AUTH_MODE=none|entra` must exist, or local evaluation requires an identity provider and adoption dies at step one. `none` binds to loopback only.

---

## F. Multi-provider support

| ADR | Decision | Status |
|---|---|---|
| ADR-015 | Four narrow ports (`IBrokerAdmin`, `IMessageTap`, `IMessagePublisher`, `IMessageInspector`) plus a provider registry; unsupported ports are declared, never thrown | Accepted |
| ADR-016 | Every adapter normalises provider death metadata into one `DeathInfo`, preserving the provider's own string as `native_reason` | Accepted |
| ADR-017 | Provider-specific screens keyed by `addressing_model`, with a generic baseline set so a provider can ship before its bespoke UI exists | Accepted |
| ADR-018 | Broker credentials are a discriminated union per provider, stored as encrypted JSON, with ambient credentials (managed identity, IAM role) as a first-class option | Accepted |

**ADR-015 — rationale.** A single `IMessageBroker` interface fails on the first provider: the platforms share vocabulary but not semantics. All three v1 providers support the same four operations, but by different means — peek is a single call on Service Bus and a get-and-requeue dance on RabbitMQ. Narrow ports keep those differences inside their adapters instead of in `if (provider == ...)` branches. **Consequence:** the capability record becomes part of the API contract, and the UI must gate on it rather than assume.

**ADR-016 — rationale.** Dead lettering is the product's core value and the least portable concept in messaging — DLX with `x-death`, DLQ sub-queue with `DeadLetterReason`, redrive policy with a receive count, dead-letter topic, or nothing. **Consequence:** the normalised `reason` powers aggregation; `native_reason` preserves what an engineer needs when debugging. Normalisation that discards detail turns a diagnostic tool into a dashboard.

**ADR-017 — rationale.** Exchanges and routing keys, topics and subscriptions, and bare queues have no common denominator; a universal UI is the lowest common denominator and is worse for every provider. **Consequence:** the frontend holds a screen registry keyed by `addressing_model`. A provider with no bespoke screen falls back to the baseline and gains screens later without blocking release — which is what stops "provider-specific screens" from meaning "write a screen set per provider before shipping one".

**ADR-018 — rationale.** Three providers need eight credential shapes: AMQP basic auth, HTTP basic, mutual TLS, SAS policy key, Entra service principal, managed identity, service-account JSON, and workload identity. A username/password column cannot represent any of the last five. Ambient credentials must be first-class because they store **no secret at all** — the most secure option should not also be the hardest to configure. **Consequence:** `credentials_enc` holds provider-specific JSON encrypted with a per-row data key under a key-encryption key from env/KMS; it is never returned by any endpoint and never logged. RabbitMQ needs **two** credential sets — AMQP and management API — which may be the same user but are not required to be.

---

## G. Agent access

| ADR | Decision | Status |
|---|---|---|
| ADR-020 | An AI assistant reaches Marites through an MCP server that is a **client of the HTTP API**, never a second backend | Accepted |
| ADR-021 | An agent acts **as the person whose credential it holds**: a credential carrying `CanExecute` may perform any operation that person could, recorded against them with `via = <assistant>`; a credential without it may only propose | Accepted (amended 10 Nov 2026) |

**ADR-020 — rationale.** A second backend would duplicate three things that must not be duplicated: the capability model, the audit obligation, and the guard chain. The HTTP API is already the product boundary (ADR-006), and every client — browser, CLI, agent — is measured against it. **Consequence:** the MCP server holds a service-principal token, maps a subset of the contract to tools, and can be deleted without changing the product. It stores nothing.

**ADR-021 — rationale.** The existing guard against a bad destructive operation is a typed string: the queue name, the environment tag. That works because a human types it slowly. A model types it perfectly, every time, so a typed confirmation given to an agent is a formality that still *looks* like a control — which is worse than no control. **Amended 10 Nov 2026.** The first answer was an approval token bound to a human session, and it was solving the wrong problem: it made a second person sign off on a request the first person had already made in the conversation. The control that actually holds is **whose credential carries the request**. An MCP credential is issued to a named user, carries that user's capabilities plus an explicit `CanExecute` grant, and every operation through it is recorded against that user with `via = <assistant>`. The assistant cannot exceed the person, cannot act for someone else, and cannot act at all on a credential an Admin has not granted `CanExecute` — which is the default. **Consequence:** the four mutating operations accept `via` on the audit row rather than an `Approval-Id` header; proposals remain, as audit rows with result `pending`, for credentials issued without `CanExecute`; the token machinery is retained only for that proposal path.

---

## Document control

Revision 0.1 records eighteen decisions, all Accepted; a review on 07 Nov 2026 added ADR-019, the agent-access work on 10 Nov 2026 added ADR-020 and ADR-021, the storage decision superseded ADR-004 with ADR-022, and the operation-ordering decision added ADR-023. No decision remains Proposed: every question they depended on has been answered in [DECISION-LOG](../01-project/decisions-and-open-questions.md). Changing an accepted decision requires a superseding ADR rather than an edit.
