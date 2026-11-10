---
document_id: DOC-API-003
title: Error Catalog
product: Marites
version: 0.1
date: 2026-11
status: Draft — codes binding on data-and-contracts/openapi/openapi.yaml
---

# DOC-API-003 — Error Catalog

**Source:** [contract conventions](contract-conventions.md) §5. Owning document for error codes.

Every error code is defined here exactly once. The contract cites these codes; it does not invent new ones inline. Codes are **stable** — a client branches on the code, never on the `detail` text, which may change wording without notice.

**Format:** `ERR-<AREA>-<NNN>`. An identifier is assigned once and never reused. A retired code is marked `Retired`, not deleted.

## 1. Structure

Every error body is `application/problem+json`:

| Field | Purpose |
|---|---|
| `code` | Stable identifier from this catalog |
| `outcome` | One of the outcome classes in DOC-API-001 §4 |
| `retryable` | Whether the identical request may be repeated |
| `detail` | Human-readable explanation of what happened |
| `remedy` | What the operator should do, present when the cause is actionable |

## 2. Common

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-CMN-001` | 400 | Rejected | no | Request body or parameters failed validation | Correct the named field |
| `ERR-CMN-002` | 404 | Denied | no | Resource does not exist, or is outside the caller's tenant | Check the identifier |
| `ERR-CMN-003` | 409 | Conflict | no | A record with this name already exists | Choose another name, or update the existing record |
| `ERR-CMN-004` | 429 | Rejected | **yes**, after `Retry-After` | Tenant rate limit exceeded | Wait, then retry |
| `ERR-CMN-005` | 409 | Conflict | no | `Idempotency-Key` reused with different parameters | Use a new key for a different request |
| `ERR-CMN-006` | 409 | Conflict | no | `If-Match` revision is stale | Re-read the record and re-apply the change |

## 3. Authentication and authorization

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-AUTH-001` | 401 | Denied | no | Token missing, expired or invalid | Sign in again |
| `ERR-AUTH-002` | 403 | Denied | no | Role lacks the capability policy for this operation | Request the role, or ask an Admin to perform it |

## 4. Capability

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-CAP-001` | 422 | Rejected | no | The connected provider cannot perform this operation | Read `ProviderCapabilities` and omit the control for this provider |

`ERR-CAP-001` exists because clients can be wrong, not because the UI should offer the action. A control that reaches the server and is refused indicates a capability check that failed to run.

## 5. Broker

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-BRK-001` | 422 | Rejected | no | Preflight is failing; capture cannot be enabled | Resolve the named failing check, then re-run preflight |
| `ERR-BRK-002` | 503 | Unavailable | **yes** | The broker could not be reached | Check network reachability and broker health |
| `ERR-BRK-003` | 422 | Rejected | no | Credential fields do not match the declared `credentialKind` | Supply the fields the kind requires |

## 6. Queue and capture

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-QUE-001` | 422 | Rejected | no | A captured-message operation targets a queue that is not currently captured; live operations on the broker are unaffected | Enable capture for this queue |
| `ERR-QUE-002` | 422 | Rejected | no | Dead-letter monitoring is unavailable because the queue has no dead-letter routing | Configure `x-dead-letter-exchange` on new queues; existing queues require recreation |
| `ERR-QUE-003` | 422 | Rejected | no | The requested capture policy would exceed the storage ceiling | Reduce retention or switch `payloadPolicy` to `sampled` or `none` |

`ERR-QUE-002` is deliberately an error rather than a zero. A dead-letter count of `0` on a queue with no dead-letter routing would read as healthy when it means unobservable.

## 7. Messages

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-MSG-001` | 404 | Denied | no | Message not found — never captured, or past row retention | Widen the time range, or accept that only counts survive beyond the capture window |
| `ERR-MSG-002` | 422 | Rejected | no | Payload was not stored for this message under the queue's `payloadPolicy` | Raise the policy to `full` or `sampled` for this queue |

## 8. Operations

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-OPR-001` | 422 | Rejected | no | Typed confirmation does not match the target | Retype the queue name exactly |
| `ERR-OPR-002` | 409 | Conflict | no | A replay is already running for this queue | Wait for it to complete, or halt it first |
| `ERR-OPR-003` | 422 | Rejected | no | The request exceeds an operation limit — batch size, rate, or target depth | Reduce the batch or the rate |
| `ERR-OPR-004` | 202 | **Unresolved** | no | An operation failed partway; some messages may have been sent, discarded or purged | Poll `/operations/{id}` and reconcile before retrying |
| `ERR-OPR-005` | 422 | Rejected | no | The proposal has expired, or was never issued for this operation (propose path only) | Ask the proposer to re-propose; a stale suggestion is not an approval |
| `ERR-OPR-006` | 403 | Denied | no | The proposal was raised for a different identity | A proposal is approved by the person it names, or not at all (ADR-021) |
| `ERR-OPR-007` | 503 | Unavailable | **yes** | The bodies could not be held for this operation, so it was **not issued**. Nothing was discarded, purged or sent | Retry once the database is reachable. The messages are untouched (ADR-023) |

**`ERR-OPR-006` is why the propose path is real.** A proposal is bound to the identity it was raised for, so a shared credential cannot approve one on someone else's behalf — the check is not "is there a proposal" but "is this proposal yours".

**`ERR-OPR-004` is `Unresolved`, not `Unavailable`.** Reporting a partial operation as a plain failure invites a retry that repeats the successful portion. The client reconciles through the operation record instead.

**`ERR-OPR-007` is a refusal, not a failure.** It is the one error in this catalog that reports that *nothing happened*, and it exists because a destructive operation that cannot be recorded must not run. Returning it is the product keeping its own rule rather than the adopter discovering it later.

## 9. Metrics

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-MET-001` | 422 | Rejected | no | The requested granularity is not available for this range | Query `/metrics/availability` and offer only what exists |

`ERR-MET-001` prevents a retained gap from rendering as zero traffic. An empty chart says "nothing happened"; the error says "this was not kept".

## 10. Audit

| Code | HTTP | Outcome | Retryable | Meaning | Remedy |
|---|---|---|---|---|---|
| `ERR-AUD-001` | 422 | Rejected | no | The payload evidence for this operation has expired, or none was captured | The audit row keeps `payloadHash`; the bodies were retained for the evidence term (90 days by default) and are gone |

**`ERR-AUD-001` is not a failure.** The audit is permanent; the evidence is not. Returning `404` would suggest the operation never happened, which is the one thing the audit exists to prevent.

## 11. Document control

Revision 0.1 establishes the initial codes. Adding a code requires amending this document first, then the contract. Codes are never renumbered and never removed — a code that stops being produced is marked `Retired` with the revision that retired it.
