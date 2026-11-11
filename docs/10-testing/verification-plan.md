---
document_id: DOC-TEST-001
title: Verification Plan
product: Marites
version: 0.1
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-TEST-001 — Verification Plan

**Source:** [requirements](../02-business/requirements.md) §3.1, §4.1; [operation coverage matrix](../13-traceability/operation-coverage-matrix.md). Owning document for verification levels, test cases and milestone gates.

Trace expected behaviour to implementation evidence without equating document validation with application tests.

## 1. Verification levels

Seven levels, defined here and cited by every other document. A level is what a case *is*, not how important it is.

| Level | What it does | Environment |
| --- | --- | --- |
| **Contract** | Black-box request/response conformance over HTTP: schema, documented statuses, error body shape | Running service, no broker required |
| **Idempotency** | Repeats a command with the same `Idempotency-Key` and asserts `AlreadyApplied` with no second effect; repeats with a stale `If-Match` and asserts `ERR-CMN-006` | Running service |
| **Dry run** | Asserts the preview returns a count and sample and changes nothing | Running service plus store |
| **Integration** | Asserts behaviour against a real broker and a real PostgreSQL (Testcontainers), including the write path | Real PostgreSQL, real broker |
| **Load** | Sustained rate, memory bound, and effect on the broker's own traffic | Real PostgreSQL, real broker, load generator |
| **Deployment** | A clean-host install, first run and default configuration | Compose host |
| **Accessibility** | Automated WCAG 2.2 AA audit plus a keyboard-only pass over both surfaces | Built frontend |

## 2. Planned test cases

Every case is `Planned`. None has been written, and no result exists. A case asserts one outcome; where a case covers several operations it is parameterised over them rather than duplicated.

| Case | Level | Milestone | Asserts | Verifies |
| --- | --- | --- | --- | --- |
| TC-001 | Contract | M0 | Every served operation, including method/body metadata, returns only documented statuses; every 2xx body validates against its schema; generated-client tooling preserves the deferred QUERY operation when promoted | FR-019, NFR-005 |
| TC-002 | Idempotency | M0 | A repeated command key returns `AlreadyApplied` with no second effect; a stale `If-Match` returns `ERR-CMN-006` | FR-012 |
| TC-003 | Integration | M0 | No response body and no log line produced during the credential lifecycle contains a credential value | NFR-003 |
| TC-004 | Deployment | M0 | `docker compose up` on a clean host serves a working UI against a test broker with one Marites container, creates its SQLite bookkeeping store, requires no adopter-supplied database, and applies the documented defaults | NFR-006, FR-023 |
| TC-005 | Integration | M0 | A broker profile is created, read, updated and deleted against a real broker, and preflight returns one result per check with a remedy per failure | FR-012, FR-013 |
| TC-006 | Integration | M1 | Depth, rates, consumers and unacked match the broker's own management reading in the same poll interval | FR-001 |
| TC-007 | Integration | M1 | Each role is refused every operation outside its capability matrix and permitted every operation inside it | FR-017 |
| TC-008 | Integration | M1 | Peek leaves depth unchanged; discard with a matching typed confirmation removes exactly the selection; a mismatch removes nothing | FR-008, FR-009, FR-016 |
| TC-009 | Integration | M1 | A replay dry run publishes nothing; execution respects `maxPerMinute`; exactly one audit row records actor, target and count | FR-010, FR-016 |
| TC-010 | Integration | M1 | Repeated get-and-requeue peeks on a quorum queue with a delivery limit either do or do not advance the delivery count — the result settles ASM-007 either way | FR-008 |
| TC-011 | Accessibility | M1 | Both surfaces pass an automated WCAG 2.2 AA audit with zero A or AA violations and are operable keyboard-only | NFR-008 |
| TC-012 | Load | M2 | 5,000 msg/s for 30 minutes with memory flat after warm-up, and the business queue's rates unchanged within 2% of a control run | NFR-001, NFR-002, FR-002 |
| TC-013 | Integration | M2 | The binary `COPY` writer round-trips every column type it writes, including `JSONB`, `bytea` and offset-bearing timestamps | FR-002 |
| TC-014 | Integration | M2 | A crash between `COMMIT` and `ack` redelivers the batch and leaves `published` unchanged, because the ledger absorbs it | FR-002, NFR-007 |
| TC-015 | Integration | M2 | Counts for a period are identical before and after every partition covering it is dropped | FR-015, NFR-007 |
| TC-016 | Integration | M2 | Minute, hour and day tiers agree; week through year are summed from day rows; an unavailable granularity returns `ERR-MET-001` | FR-004, FR-020, FR-021 |
| TC-017 | Integration | M2 | At the ceiling, row writes stop, rollups keep incrementing, and the queue reports `captureState = capped` | FR-022, FR-023 |
| TC-018 | Integration | M2 | A stalled worker while depth grows reports `captureState = stale`; a bounded shadow queue overflowing raises the capture-gap warning | FR-018, FR-002 |
| TC-019 | Integration | M2 | Each provider's death metadata maps to one `deathReason` with the provider's own string preserved in `nativeReason`, and reasons sum to `deadLettered` | FR-003, FR-005 |
| TC-020 | Integration | M3 | Filters combine conjunctively, ordering is `capturedAt` descending, and a payload under `full` is byte-identical to what was published | FR-006, FR-007 |
| TC-021 | Integration | M3 | An invalid body publishes nothing; one idempotency key publishes once; two keys publish twice | FR-011 |
| TC-022 | Integration | M4 | A cross-tenant read returns `ERR-CMN-002` and never a partial resource | FR-017 |
| TC-023 | Integration | M4 | One unreachable cluster leaves the others' figures and health unchanged | FR-014 |
| TC-024 | Integration | M3 | A credential without `CanExecute` reads and creates a proposal, and a direct mutating call returns `ERR-AUTH-002`; a credential with `CanExecute` executes a replay and the audit row names the user with `via` set; a Support credential with `CanExecute` is refused on purge; no credential can act for a different user | FR-024 |
| TC-025 | Integration | M2 | With `captureMode = counts`, `published` increments for every message, `message_capture` stays empty for that queue, and a message search on it returns an empty page rather than an error | FR-025 |
| TC-026 | Integration | M3 | With no provider configured the drawer transmits nothing; configured, an answer cites a queue and a count; a replay asked for in the drawer executes only under `CanExecute` and is audited against the signed-in user with `via`; a Viewer cannot open it | FR-026 |
| TC-028 | Integration | M1 | With the database made read-only, a discard returns `ERR-OPR-007` and the message is still on the broker; with the process killed between the acknowledgement and the audit write, restart reconciliation produces exactly one audit row for the operation; a command the broker rejects leaves an `abandoned` intent and no success row | FR-028 |
| TC-029 | Integration | M2 | Compaction of one window run twice leaves every column identical to a single pass; the process is killed mid-transaction on three consecutive runs, and each restart leaves the window either untouched or at the correct totals — never at a partial sum; a read committed between the two halves of the transaction never sees a finer tier that is partly deleted | FR-029 |
| TC-027 | Integration | M1 | A purge of 10 messages yields 10 evidence rows with their bodies; an edited replay returns `payloadBefore` and `payloadAfter`, and its audit row reports `payloadEdited` while an unedited replay's does not; a single-message replay returns its body whole, byte for byte; a peek returns what it showed; after the evidence retention lapses the call returns `ERR-AUD-001` while `payloadHash` and `payloadEdited` survive; a queue with 3-day row retention still holds its discard evidence at day 30, and a queue with 200-day row retention holds it at day 120; beyond the row bound `payloadTruncated` is true | FR-027 |

## 3. Milestone gates

A milestone does not exit until its gate passes. Each gate names the cases that must be green and the document that would have to change if one fails.

| Gate | Cases | Milestone | If it fails |
| --- | --- | --- | --- |
| Contract conformance | TC-001, TC-002 | M0 | The contract or the conventions are wrong; fix the owner, then the contract |
| Deployment smoke | TC-004 | M0 | NFR-006 or the default set is wrong; fix before M1 builds on it |
| Live console | TC-006, TC-007, TC-008, TC-009, TC-010 | M1 | M1's exit criteria are unmet: Support cannot complete a recovery |
| Capture | TC-012, TC-013, TC-014, TC-018, TC-019 | M2 | **ADR-011 or ADR-019 is wrong.** A failure here invalidates the counting claim, so it needs a superseding ADR, not a patch |
| Retention and reporting | TC-015, TC-016, TC-017, TC-029 | M2 | The two-tier design or the degradation order is wrong; DOC-DATA-001 §3–§4 changes first |
| Explorer | TC-020, TC-021 | M3 | The message contract is wrong |
| Agent access | TC-024 | M3 | **ADR-021 is wrong.** If a credential can exceed the role of the person it was issued to, `CanExecute` is a global switch wearing a per-user costume |
| Tenancy | TC-022, TC-023 | M4 | ADR-010 or ADR-014 is wrong |

**Two gates exist because of accepted decisions and cannot be satisfied by inspection.** TC-013 and TC-014 assert ADR-011 (binary `COPY`) and ADR-019 (redelivery-safe counting). Both are load-bearing for the "exact counts" promise, both are integration-level, and neither can be argued from a document. That is why they are gates rather than optional tests.

## 4. What is not evidence

A green document check is not an application test. Link resolution, identifier resolution and OpenAPI validation run on every change to this documentation set, and they establish only that the documents agree with themselves. No case above has been executed, no result exists, and no deployment has been performed.

## 5. Document control

Revision 0.1 establishes the level vocabulary, 29 planned cases and eight milestone gates. Cases are added, never renumbered; a case that stops being relevant is marked `Withdrawn` with its reason. No case has a result, and no level has been exercised.
