---
document_id: DOC-SRS-001
title: Software Requirements Specification
product: Marites
version: 0.2
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-SRS-001 — Software Requirements Specification

**Source:** discovery conversation, November 2026. Owning document for scope and priorities. Enumerated values are cited from [VOCABULARY](../14-reference/vocabulary.md), never restated here.

## 1. Purpose and scope

Marites is a self-hosted web application that observes message traffic crossing one or more messaging platforms and lets authorised operators inspect, count, and act on that traffic — including dead-lettered messages.

It is a **client**. It connects to brokers the adopter already runs. It does not provision, host, upgrade, or repair them, and it does not create or modify dead-letter topology.

**This specification defines the approved scope, and gaps are review findings.** Every currently approved behaviour should be traceable through these requirements and their owning specifications. A behaviour not described in the set is not implied and must not be added by implementation without review. The list in §6 names omissions a reader is most likely to look for; it is not a guarantee that the set has no other gaps. When a reader finds one, reconcile the owning requirement or record the behaviour as out of scope before implementation.

## 2. Actors and the primary workflow

**Support is the primary actor.** The product exists so that a support engineer, mid-incident, can find a failing queue, understand what is failing and why, and fix it — retry, discard, or publish — without SSH, without the RabbitMQ management UI, and without a DBA.

Dashboards, counting, and history exist to serve that loop. They are not the product.

| Actor | Description | Primary |
|---|---|---|
| Adopter | Installs Marites on their own server and supplies the broker. Not a user of the UI. | — |
| **Support** | Viewer plus operational recovery: peek, discard, replay, publish. | **Yes** |
| Viewer | Executive reporting. Reads aggregate volume, trends, and dead-letter rate. Sees no message bodies and performs no broker mutation. | — |
| Admin | Full control including purge, broker profiles, capture rules, and role assignment. | — |

### The loop the product must close

| Step | What Support needs | Owning requirement |
|---|---|---|
| Detect | Queue depth rising, dead-letter count climbing, capture healthy | FR-001, FR-004, FR-018 |
| Understand | What failed, why, and with what payload | FR-005, FR-006, FR-007 |
| Act | Retry, discard, or publish — in place, safely | FR-008…FR-011 |
| Prove | An audit record of what was done, by whom | FR-016 |

A feature that does not serve one of these four steps is not MVP.

### Two audiences, two surfaces

The product serves two different people, and conflating them produces a UI that suits neither.

| Surface | Audience | Question they arrive with |
|---|---|---|
| **Operations console** | Support, Admin | "Something is failing — what is it and how do I fix it?" |
| **Reporting dashboard** | Viewer (executive) | "How much traffic is the company processing, and how much is failing?" |

The reporting dashboard exists because Viewer is a **volume and health** audience, not an operations audience. It shows aggregate counts, trends, and dead-letter rate over weeks or quarters — and deliberately no message bodies, because an executive needs the number, not the payload, and payloads carry PII and tokens.

This is the second reason the capture pipeline separates counting from storage ([DOC-DATA-001 §3](../07-data-and-contracts/data-model.md)): the reporting surface reads rollups only and never scans captured rows, so a twelve-month question costs the same whether one million or one billion messages were stored.

**Sequencing consequence.** Steps Detect and Act do not require the capture pipeline — they operate on the live queue through the broker API. Only *historical* action on a specific captured message requires storage. Therefore live operations belong in M1, not M4 (DOC-OPS-001 §6).

Role definitions and their capability grants are owned by [VOCABULARY §2](../14-reference/vocabulary.md) and [DOC-UX-001 §2](../08-experience/dashboard-and-operations.md).

## 3. Functional requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-001 | Show queue metadata per watched queue: depth, publish/deliver/ack rates, consumer count, unacked. | Must |
| FR-002 | Capture every message crossing a watched exchange, not only dead letters, and count all of them exactly (where the provider reports redelivery; otherwise at-least-once, ADR-019). | Must |
| FR-003 | Identify dead-lettered messages and record the reason from `x-death`. | Must |
| FR-004 | Show total published and total dead-lettered per queue over a selectable window, plus the derived dead-letter rate. | Must |
| FR-005 | Break the dead-letter count down by reason. | Must |
| FR-006 | List and filter captured messages by routing key, death reason, correlation id, and time. Support and Admin only — Viewer has no message-level access (DEC-002). | Must |
| FR-007 | Display a message payload, headers, and `x-death` detail. Support and Admin only (DEC-002). | Must |
| FR-008 | Peek at messages still sitting in a queue without permanently consuming them. | Must |
| FR-009 | Discard a single captured message or purge a queue, with typed confirmation. | Must |
| FR-010 | Replay dead-lettered messages to their original exchange and routing key, with dry-run preview and rate limiting. | Must |
| FR-011 | Publish an arbitrary message to an exchange, with validation, idempotency key, and audit. | Must |
| FR-012 | Register, test, and remove broker connection profiles. | Must |
| FR-013 | Run a preflight readiness check against a broker and report per-item pass/fail. | Must |
| FR-014 | Support multiple broker clusters simultaneously, grouped and labelled by environment. | Must |
| FR-015 | Enforce an admin-set per-queue retention policy for captured rows and payloads, without ever affecting recorded counts. | Must |
| FR-016 | Record every operation that touches the broker or changes configuration in an append-only audit log — including the read-like ones, `peek` and `preflight`. | Must |
| FR-017 | Map authenticated identity to one of the three roles and enforce capability policies. | Must |
| FR-018 | Report capture health per queue so a stopped pipeline is distinguishable from quiet traffic. | Must |
| FR-019 | Expose the full API surface as an OpenAPI contract, from which the frontend client is generated. | Must |
| FR-020 | Report volume and dead-letter counts at day, week, month, quarter and year granularity across the retained reporting horizon. | Must |
| FR-021 | Report which granularities are available for a requested range, so the UI never renders a retained-gap as an empty chart. | Must |
| FR-022 | Continue writing rollups and reporting counts when row storage reaches its ceiling, and surface the condition as "capped" rather than as zero traffic. | Must |
| FR-023 | Apply conservative defaults to every storage-affecting setting so the product is safe and functional before any configuration. | Must |
| FR-024 | Let an AI assistant read through a machine interface and, when its credential carries `CanExecute`, **execute** operations as the person who holds that credential — recorded against that person with the channel noted. A credential without `CanExecute` may only propose. | Should |
| FR-025 | Allow a queue to be counted without storing any of its messages, so an adopter can keep exact counts and decline message storage entirely. | Should |
| FR-026 | Let an administrator connect an AI provider — including a local runtime — and give Support a hosted assistant that reads and, when the signed-in user's credential carries `CanExecute`, acts as that user within their role. | Should |
| FR-028 | Hold the complete bodies durably before issuing a destructive command, and write the audit row only after the broker confirms. A command whose bodies cannot be held is not issued. | Must |
| FR-029 | Compact a rollup tier by recomputing the target window's absolute totals from the finer tier and assigning them, so a compaction that runs twice for one window produces the same row — and commit the coarser write together with the deletion of the finer rows it covers, so a partly deleted tier cannot exist. | Must |
| FR-027 | Keep **complete** payload evidence for peek, replay, discard and purge — one record per message touched, with the body before and, for a replay, after — retained on its own clock (`max(row retention, the evidence retention)`) and never inside the audit row. | Must |

### 3.1 Acceptance criteria

Each requirement has exactly one testable criterion and one outcome. A criterion that needs two outcomes has been split, or the requirement is doing two things and should be split.

**Milestone** is the first milestone at which the criterion can pass ([DOC-OPS-001 §6](../11-operations/deployment-and-onboarding.md)). An operation may be served earlier with a subset of its fields; that is not the same as the requirement being met.

**Case** cites a planned test case from the [testing index](../10-testing/README.md). Every case is `Planned`: none has been written, and nothing has been executed.

| Requirement | Milestone | Acceptance criterion | Case |
|---|---|---|---|
| FR-001 | M1 | For a watched queue, the queue list and detail return depth, publish/deliver/ack rates, consumer count and unacked count, and each value equals the broker's own management reading taken in the same poll interval | TC-001, TC-006 |
| FR-002 | M2 | After publishing 1,000 distinct messages to a watched exchange, `published` reads exactly 1,000; after a forced crash between `COMMIT` and `ack` it still reads exactly 1,000 | TC-012, TC-014, TC-018 |
| FR-003 | M2 | A message rejected without requeue is stored once with `isDeadLetter = true`, `deathReason = rejected` and a non-empty `nativeReason` | TC-019 |
| FR-004 | M2 | For a window, `published`, `deadLettered` and `deadLetterRate` are returned, the rate equals `deadLettered / published` (0 when published is 0), and the per-queue rows sum to the reported total | TC-016 |
| FR-005 | M2 | The dead-letter breakdown returns one row per distinct `deathReason` in the window and those rows sum to `deadLettered` | TC-019 |
| FR-006 | M3 | A search returns only messages matching every supplied filter, ordered `capturedAt` descending, and the same request as Viewer returns `ERR-AUTH-002` | TC-020, TC-001 |
| FR-007 | M3 | Message detail returns headers, `x-death` detail and a payload whose decoded bytes are identical to what was published under `payload_policy = full` | TC-020 |
| FR-008 | M1 | A peek returns up to the requested number of messages with payloads, the target queue's depth is unchanged afterwards, and the response states `destructive` correctly for the provider | TC-008, TC-010 |
| FR-009 | M1 | Discard with a matching typed confirmation removes exactly the selected messages; a mismatched confirmation returns `ERR-OPR-001` and removes nothing | TC-008 |
| FR-010 | M1 | A replay dry run returns the count and death reasons and publishes nothing; execution publishes at no more than `maxPerMinute`; exactly one audit row records actor, target and count | TC-009 |
| FR-011 | M3 | A body failing content-type validation returns `ERR-CMN-001` and publishes nothing; a valid body publishes once per idempotency key and twice for two different keys | TC-021 |
| FR-012 | M0 | A broker profile can be created, read, updated and deleted, and no response in that sequence contains a credential value | TC-005, TC-003 |
| FR-013 | M0 | Preflight returns one result per check with pass/fail and a remedy on each failure, and enabling capture while a mandatory check fails returns `ERR-BRK-001` | TC-005 |
| FR-014 | M4 | Two profiles in different environments are reported separately, each with its own health, and making one unreachable leaves the other's figures unchanged | TC-023 |
| FR-015 | M2 | Lowering row retention drops the partitions past the new window and leaves every `message_rollup` total unchanged | TC-015 |
| FR-016 | M1 | Every mutating operation writes exactly one `operation_audit` row with actor, action, target, result and count, and no endpoint can modify or delete one | TC-008, TC-009 |
| FR-017 | M1 | Each of the three roles is refused every operation outside its capability matrix with `ERR-AUTH-002` and permitted every operation inside it | TC-007 |
| FR-018 | M2 | A queue whose capture stops while its depth grows reports `captureState = stale` with a last-seen timestamp, and its count is never reported as zero | TC-018 |
| FR-019 | M0 | The served contract validates as OpenAPI 3.2.1, the generated client compiles, and renaming a contract field fails the frontend build | TC-001 |
| FR-020 | M2 | A day, week, month, quarter and year query over the same period returns totals that agree to the message | TC-016 |
| FR-021 | M2 | For a range whose finer tiers have been compacted, availability reports only the tiers that exist, and requesting an unavailable one returns `ERR-MET-001` | TC-016 |
| FR-022 | M2 | With the ceiling reached, `published` and `deadLettered` keep incrementing while row writes stop, and the queue reports `captureState = capped` | TC-017 |
| FR-023 | M2 | A fresh install with no capture configuration stores sampled payloads at 1 in 100, keeps rows 72 hours, partitions daily, and applies a ceiling of `min(50 GB, 20% of available disk)` | TC-004, TC-017 |
| FR-029 | M2 | Compacting the same window twice leaves `published`, `dead_lettered`, `bytes` and `peak_rate` identical to one pass; a process killed during the compaction transaction, then restarted, leaves the window either unchanged or at the correct single-pass values and never at a partial sum; the coarser write and the finer deletion are never observed separately | TC-029 |
| FR-028 | M1 | A discard whose hold cannot be written returns `ERR-OPR-007` and the message is still on the broker, unchanged; a discard that succeeds writes its audit row and evidence only after the acknowledgement; a process killed between the command and the record leaves an intent that reconciliation resolves into exactly one audit row, never zero and never two | TC-028 |
| FR-027 | M1 | A purge of 10 messages yields 10 evidence rows, each with its own body; a replay whose body was edited returns both `payloadBefore` and `payloadAfter` and the audit row for it reports `payloadEdited` true before the row is expanded; a single-message replay returns its body whole, not truncated; a peek returns the bodies it showed; after the evidence retention lapses the same call returns `ERR-AUD-001` while `payloadHash` survives, and the retention is `max(row retention, the evidence retention)` so a three-day queue keeps destructive evidence for the full term; beyond `MARITES_EVIDENCE_MAX_ROWS` the rows stop and `payloadTruncated` is true | TC-027 |
| FR-026 | M3 | With no provider configured the drawer says so and transmits nothing; once configured, an answer cites queue and count; a replay asked for in the drawer is executed only when the signed-in user's credential carries `CanExecute`, and the audit row names that user with `via` set; a Viewer has no access to the drawer | TC-026 |
| FR-025 | M2 | With `captureMode = counts`, `published` increments for every message and `message_capture` receives no row for that queue; a message search on it returns an empty page, not an error | TC-025 |
| FR-024 | M3 | A credential without `CanExecute` can read and propose, and every direct mutating call returns `ERR-AUTH-002`; a credential with `CanExecute` executes a replay that the same user could perform in the UI, and the audit row names the user with `via` set; a Support credential with `CanExecute` still cannot purge, and no credential can act for a different user | TC-024 |

## 4. Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-001 | Sustained capture of at least 5,000 messages/second per instance without unbounded memory growth. |
| NFR-002 | Capture must not apply backpressure to, or degrade, the adopter's production consumers. |
| NFR-003 | Broker credentials encrypted at rest; never logged, never returned by any API response. |
| NFR-004 | No destructive broker operation may occur without explicit human confirmation. No scheduled destructive operations, ever. |
| NFR-005 | The HTTP contract is the product boundary; a second implementation in another language must be able to satisfy it without changing the frontend. |
| NFR-006 | Single-server deployment must run from one container, with Marites' own bookkeeping store created automatically, and an **optional adopter-supplied message store** — PostgreSQL or SQL Server (DEC-012, ADR-022). |
| NFR-007 | Exact counts must survive row-level retention expiry. Exact means each delivered copy once, where redelivery is detectable (ADR-019). |
| NFR-008 | Both surfaces must be operable keyboard-only and meet WCAG 2.2 level AA, because the operations console is used under incident pressure. |

### 4.1 Acceptance criteria

| Requirement | Milestone | Acceptance criterion | Case |
|---|---|---|---|
| NFR-001 | M2 | A 30-minute capture at 5,000 msg/s sustains throughput with process memory flat within 10% after warm-up and no unbounded growth in the batch buffer | TC-012 |
| NFR-002 | M2 | During that run, the business queue's publish and delivery rates match a control run with Marites detached, within 2% | TC-012 |
| NFR-003 | M0 | No API response body and no log line produced during the credential lifecycle contains a credential value, including partially masked | TC-003 |
| NFR-004 | M1 | Every destructive operation refuses without a valid confirmation token, and no scheduled component invokes one — verified by inspecting the registered hosted services | TC-008, TC-009 |
| NFR-005 | M0 | The conformance suite passes against the served contract with no frontend change, and the frontend builds from the generated client alone | TC-001 |
| NFR-006 | M0 | `docker compose up` on a clean host yields a working UI against a test broker with no adopter-supplied database at all — Marites creates its own bookkeeping store and the product is fully operable, in live-only mode | TC-004 |
| NFR-007 | M2 | Counts for a period are identical before and after every partition covering that period is dropped | TC-015, TC-014 |
| NFR-008 | M1 | Both surfaces are operable with the keyboard alone, and an automated audit reports zero WCAG 2.2 level A or AA violations | TC-011 |

## 5. Constraints

**C-001 — AMQP has no peek.** A queue is a destructive read stream. Consuming removes the message, and two consumers on one queue compete rather than both observing. This constraint drives the entire capture architecture (DOC-ARCH-001 §3) and cannot be designed away.

**C-002 — Capture is forward-only.** A shadow binding observes messages published after it exists. Messages already in a queue require a separate, rate-limited backfill.

**C-003 — The adopter supplies a configured broker.** See DOC-OPS-001 §1. A misconfigured broker produces an application that looks broken, which is why FR-013 exists.

## 6. Out of scope for v1

- Provisioning, hosting, upgrading, or repairing RabbitMQ brokers
- Creating or altering dead-letter topology, including adding `x-dead-letter-exchange` to an existing queue
- Kafka, Amazon SQS and Amazon SNS (DEC-001), and any platform not listed in VOCABULARY §12
- Operating a hosted SaaS for third parties — this is self-hosted software; multi-tenancy means one install serving several teams inside one organisation
- Alerting and paging integrations
- Message flow topology graph
- Kafka or SQS adapters
- Analytics beyond the retention window

## 7. Traceability

Designed verification exists as named cases; no test has been written, no code exists, and no behaviour has been observed against a broker. Case identifiers are defined once in the [testing index](../10-testing/README.md); this table records ownership and the case that would prove each requirement. Per-operation mapping is in the [operation coverage matrix](../13-traceability/operation-coverage-matrix.md).

| Requirement group | Owning specification | Cases |
|---|---|---|
| FR-001, FR-012–FR-014 | DOC-OPS-001 §1–§2, §6; DOC-ARCH-001 §9–§10 | TC-005, TC-006, TC-023 |
| FR-002–FR-005 | DOC-DATA-001 §3 | TC-012, TC-014, TC-016, TC-018, TC-019 |
| FR-006–FR-007 | DOC-UX-001 §1; DOC-API-002 §4 | TC-020 |
| FR-008–FR-011 | DOC-UX-001 §3; DOC-API-002 §6 | TC-008, TC-009, TC-010, TC-021 |
| FR-015, FR-020, FR-021 | DOC-DATA-001 §3.1.1–§3.1.2, §4 | TC-015, TC-016 |
| FR-016 | DOC-DATA-001 §2, §4 | TC-008, TC-009 |
| FR-017 | DOC-UX-001 §2 | TC-007 |
| FR-018 | DOC-UX-001 §1 rule 4; DOC-ARCH-001 §2 | TC-018 |
| FR-019 | DOC-ARCH-001 §5; DOC-API-001 | TC-001 |
| FR-022 | DOC-DATA-001 §4.1 | TC-017 |
| FR-023 | DOC-DATA-001 §4; DEC-005 | TC-004, TC-017 |
| NFR-001 | DOC-DATA-001 §3, §5; DOC-ARCH-001 §7 | TC-012 |
| NFR-002 | DOC-ARCH-001 §2, §7 | TC-012 |
| NFR-003 | DOC-DATA-001 §2; ADR-018 | TC-003 |
| NFR-004 | DOC-UX-001 §3; DOC-API-001 §3 | TC-008, TC-009 |
| NFR-005 | DOC-API-001; DOC-ARCH-001 §5 | TC-001 |
| NFR-006 | DOC-OPS-001 §3 | TC-004 |
| NFR-007 | DOC-DATA-001 §3.1.1; ADR-019 | TC-014, TC-015 |
| NFR-008 | DOC-UX-002 §2 | TC-011 |
| FR-024 | DOC-ARCH-004; ADR-020, ADR-021 | TC-024 |
| FR-025 | DOC-DATA-001 §3; VOCABULARY §16 | TC-025 |
| FR-026 | DOC-ARCH-004 §9; VOCABULARY §17; DEC-015 | TC-026 |
| FR-027 | DOC-DATA-001 §2, §4; DEC-017, DEC-018 | TC-027 |
| FR-028 | DOC-DATA-001 §2.2; DOC-ARCH-006 §4; DOC-ARCH-007 §7; ADR-023 | TC-028 |
| FR-029 | DOC-DATA-001 §2.2, §4; ADR-024 | TC-029 |

## 8. Document control

Revision 0.2 records the initial requirement set and the complete requirement-to-specification crosswalk. Priorities assume the MVP boundary in DOC-OPS-001 §6. All nineteen decisions in [DECISION-LOG](../01-project/decisions-and-open-questions.md) are answered; none blocks M0.

*Amended November 2026:* acceptance criteria added for every FR (§3.1) and NFR (§4.1), each with one outcome and a milestone; §7 now cites planned test cases rather than reading "not yet designed". **NFR-008 (accessibility) added**, because both surfaces are used under incident pressure and keyboard-only operation was recorded as a gap in the experience section with no requirement behind it. No existing requirement changed text or priority.
