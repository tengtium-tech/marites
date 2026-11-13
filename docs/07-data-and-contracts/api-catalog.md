---
document_id: DOC-API-002
title: API Catalog
product: Marites
version: 0.4
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-API-002 — API Catalog

**Source:** [contract conventions](contract-conventions.md), [error catalog](error-catalog.md). Generated view of `07-data-and-contracts/openapi/openapi.yaml`.

**Counts:** 28 operations designed — **17 in the served slice (v0.4)**, 11 deferred. Of the 17 active, 7 are queries and 10 are commands. Capability names are owned by [VOCABULARY §2](../14-reference/vocabulary.md); role grants by [DOC-UX-001 §2](../08-experience/dashboard-and-operations.md).

This table exists so the API can be read without opening the contract. If the two disagree, the contract is authoritative and this table is corrected.

## Scope by revision

Only the served operations are implemented; the rest are designed and preserved in `07-data-and-contracts/openapi/openapi.deferred.yaml`; promoting one means moving it into `openapi.yaml` and updating this table — a move, not a rewrite.

| Revision | Operations |
|---|---|
| **v0.1 — served** | `getBackendCapabilities`, `listBrokers`, `createBroker`, `getBroker`, `updateBroker`, `deleteBroker`, `runPreflight`, `getProviderCapabilities`, `listQueues`, `getQueue` |
| **v0.3 — served** | `peekQueue`, `discardMessages`, `purgeQueue`, `replayMessages` (live redrive) — the four recovery operations, acting on the live queue because no capture store exists before M2 |
| **v0.4 — served** | `signInWithGoogle`, `getSession`, `signOut` — the Google sign-in exchange, session read and logout; they exist only when `MARITES_AUTH_MODE=google` and answer `404` in the other modes |
| M1 (remaining) | `getOperation`, `listAudit`, `listAuditPayloads` |
| M3 | `createProposal` — the agent surface (DEC-010) |
| M2 | `setCapturePolicy`, `getMetricSummary`, `getMetricSeries`, `getMetricAvailability` |
| M3 | `searchMessages`, `getMessage`, `publishMessage`; `replayMessages` gains captured-message replay |

**Why the slice stops here.** Ten operations cover onboarding end to end — register a broker, verify it, read its queues — which is a demonstrable product; the four v0.3 operations make recovery possible against a live broker and still need no capture; the three v0.4 operations are what Google mode needs to turn a verified Google account into a Marites session. Everything below that line is deferred and depends on capture data that does not exist yet. Milestone numbers follow [DOC-OPS-001 §6](../11-operations/deployment-and-onboarding.md), which owns them.

## 1. Capabilities

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-CAP-001 | GetBackendCapabilities | `GET /capabilities` | query | All | — | none |
| OP-CAP-002 | GetProviderCapabilities | `GET /brokers/{brokerId}/capabilities` | query | All | — | none |

`OP-CAP-002` is what makes provider-specific screens possible: the client reads it and renders only the controls the provider can honour.

## 2. Brokers

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-BRK-001 | ListBrokers | `GET /brokers` | query | All | — | none |
| OP-BRK-002 | CreateBroker | `POST /brokers` | command | Admin | `CanManageBrokers` | Idempotency-Key |
| OP-BRK-003 | GetBroker | `GET /brokers/{brokerId}` | query | All | — | none |
| OP-BRK-004 | UpdateBroker | `PATCH /brokers/{brokerId}` | command | Admin | `CanManageBrokers` | Idempotency-Key, If-Match |
| OP-BRK-005 | DeleteBroker | `DELETE /brokers/{brokerId}` | command | Admin | `CanManageBrokers` | Idempotency-Key |
| OP-BRK-006 | RunPreflight | `POST /brokers/{brokerId}/preflight` | command | Admin | `CanManageBrokers` | Idempotency-Key |

Viewer may list brokers because the reporting dashboard groups volume by cluster; it may not see or change their configuration beyond the name and environment.

`OP-BRK-005` removes the observation objects Marites created in that broker — shadow queues, bindings or subscriptions. It never touches the adopter's own topology.

## 3. Queues

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-QUE-001 | ListQueues | `GET /queues` | query | All | — | none |
| OP-QUE-002 | GetQueue | `GET /queues/{queueId}` | query | All | — | none |
| OP-QUE-003 | SetCapturePolicy | `PUT /queues/{queueId}/capture-policy` | command | Admin | `CanManageCaptureRules` | Idempotency-Key, If-Match |

`OP-QUE-003` returns the projected storage cost alongside the applied policy, so an expensive choice is visible at the moment it is made rather than on a later invoice.

## 4. Messages

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-MSG-001 | SearchMessages | `QUERY /messages` | query | Support, Admin | `CanViewPayloads` | structured JSON filter/sort/page |
| OP-MSG-002 | GetMessage | `GET /messages/{messageId}` | query | Support, Admin | `CanViewPayloads` | none |

**Viewer receives 403 on both** (DEC-002). There is no redacted-payload variant, so no Viewer-accessible branch exists to maintain.

## 5. Metrics

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-MET-001 | GetMetricSummary | `GET /metrics/summary` | query | All | — | none |
| OP-MET-002 | GetMetricSeries | `GET /metrics/timeseries` | query | All | — | none |
| OP-MET-003 | GetMetricAvailability | `GET /metrics/availability` | query | All | — | none |

`OP-MET-003` exists so the client offers only granularities that actually exist for a range. Without it, a per-minute request for six months ago renders an empty chart that reads as "no traffic".

## 6. Operations

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-OPR-001 | PeekQueue | `POST /operations/peek` | command | Support, Admin | `CanPeek` | Idempotency-Key |
| OP-OPR-002 | DiscardMessages | `POST /operations/discard` | command | Support, Admin | `CanDiscard` | Idempotency-Key, dry run, confirmation |
| OP-OPR-003 | PurgeQueue | `POST /operations/purge` | command | Admin | `CanPurge` | Idempotency-Key, dry run, confirmation, environment confirmation |
| OP-OPR-004 | ReplayMessages | `POST /operations/replay` | command | Support, Admin | `CanReplay` | Idempotency-Key, dry run, environment confirmation (prod) |
| OP-OPR-005 | PublishMessage | `POST /operations/publish` | command | Support, Admin | `CanPublish` | Idempotency-Key, environment confirmation (prod) |
| OP-OPR-006 | GetOperation | `GET /operations/{operationId}` | query | Support, Admin | — | none |
| OP-OPR-007 | CreateProposal | `POST /operations/proposals` | command | Service principals (no `CanExecute`) | `CanPropose` | Idempotency-Key |

`OP-OPR-007` is the path for a credential that may not act: it changes a record of intent and
executes nothing, returning a proposal id and an expiry. A credential carrying `CanExecute` calls the
four mutating operations directly — authorised against the role of the person it was issued to, and
recorded against that person with `via` set ([ADR-021](../05-architecture/decisions.md)). The
`Approval-Id` header exists only on the propose path.

`OP-OPR-004` and `OP-OPR-005` are the highest-risk surface in the product: one re-sends messages into a live consumer, the other injects a new message into a business flow. Both are rate limited, both are audited, and `OP-OPR-004` auto-halts if the target queue grows faster than it drains.

## 7. Authentication

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-AUTH-001 | SignInWithGoogle | `POST /auth/google` | command | Anyone with a Google account | — | Google ID token validation; explicit `(issuer, subject)` mapping |
| OP-AUTH-002 | GetSession | `GET /auth/session` | query | Any signed-in user | — | session cookie |
| OP-AUTH-003 | SignOut | `POST /auth/logout` | command | Any signed-in user | — | session cookie, CSRF token |

`OP-AUTH-001` is the only operation in this API that accepts an unauthenticated caller, and it accepts exactly one thing: a Google ID token. It is served only in `google` mode; `entra` and `none` answer `404`, because an endpoint that cannot work in the configured mode should not appear to exist. A valid token with no mapping is `403 ERR-AUTH-002` — the identity is real, the permission is not.

`OP-AUTH-002` is a read, so it is not audited: it changes nothing. `OP-AUTH-003` revokes every session the user holds and is idempotent. Neither is available in `entra` mode, where the bearer token's own expiry is the session.

## 8. Audit

| ID | Operation | Method and path | Kind | Roles | Capability | Guards |
|---|---|---|---|---|---|---|
| OP-AUD-001 | ListAudit | `GET /audit` | query | Support, Admin | `CanViewAuditLog` | none |
| OP-AUD-002 | ListAuditPayloads | `GET /audit/{auditId}/payloads` | query | Support, Admin | `CanViewAuditLog`, `CanViewPayloads` | none |

Append-only. No operation in this API modifies or deletes an audit entry.

`OP-AUD-002` returns the bodies snapshotted when the operation ran — what was sent, or what was destroyed — for as long as the queue's row retention allows. After that it returns `ERR-AUD-001` and the audit row keeps `payloadHash` and `payloadEdited` (DEC-017).

## 9. Operations by risk

| Risk | Operations | Mitigation |
|---|---|---|
| Injects data into a business flow | OP-OPR-005 | Idempotency key, capability gate, audit |
| Re-sends messages to consumers | OP-OPR-004 | Dry run, rate limit, auto-halt, audit |
| Removes messages irreversibly | OP-OPR-002, OP-OPR-003 | Typed confirmation, dry run, Admin-only for purge, audit |
| Mutates broker topology | OP-BRK-002, OP-BRK-005, OP-BRK-006 | Admin only. Create and recheck may create and clean up a temporary probe. Delete cleans up Marites-owned topology. Scope is limited to the `marites.` prefix |
| Reads sensitive content | OP-MSG-001, OP-MSG-002 | Role gate, tenant scope, no Viewer access |
| Records intent without effect | OP-OPR-007 | Writes a `pending` audit row; executes nothing |
| Admits an unmapped identity | OP-AUTH-001 | Explicit `(issuer, subject)` mapping; no default role; `403 ERR-AUTH-002` |
| None | All queries | — |

## 10. Document control

Revision 0.2 records the split: ten operations are served in `openapi.yaml`, fifteen are preserved in `openapi.deferred.yaml`. OP-MSG-001 is a deferred safe/idempotent `QUERY`; no `POST /search` operation exists. The Codex documentation review on 2026-11-11 reconciled the catalog milestones with both contract files and the operation matrix. Regenerating this table is a manual step; when a contract changes, this document is corrected in the same commit.

Revision 0.3 promotes the four recovery operations — `peekQueue`, `discardMessages`, `purgeQueue`, `replayMessages` — into `openapi.yaml`, adapted to run before capture exists: the messages come from the live queue, the identifiers a peek returns are broker-assigned and opaque, and discard and replay name their target queue directly. The deferred `MessageSummary`/`MessageDetail` shapes, which assume captured rows, stay deferred until M2/M3.

Revision 0.4 adds the three sign-in operations and the `sessionCookie` security scheme (ADR-008 amendment). The capability matrix is unchanged: Google and Entra both map into Viewer, Support and Admin, and the provider decides identity while the explicit mapping decides role.
