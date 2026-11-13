# Operation coverage matrix

Version 0.3 | November 2026 | Status: Approved — Product owner, 2026-11-11

Counts: 28 operations, 17 served, 11 deferred, 28 tied to a requirement and at least one case, 0 with an executed verification

This matrix connects every operation in the [API catalog](../07-data-and-contracts/api-catalog.md) to the requirement it serves, the role allowed to call it, its milestone, the verification level and the cases that would prove it. Verification remains Planned: no test code has been written or executed, and no operation exists as running software. Levels and cases are defined once in the [verification plan](../10-testing/verification-plan.md); this table cites them.

How it was derived. Operation, method, path and roles are read from the catalog tables. The requirement column is a reviewable judgment: each operation is assigned to the requirement whose text it performs. A reviewer who disagrees should change the row and say why. Milestones follow [DOC-OPS-001 §6](../11-operations/deployment-and-onboarding.md), which owns them. FR-017 (role enforcement) applies to every operation and is not repeated per row; TC-007 covers it once.

| ID | Operation | Method and path | Requirement | Roles | Milestone | Planned verification | Cases |
| --- | --- | --- | --- | --- | --- | --- | --- |
| OP-CAP-001 | GetBackendCapabilities | `GET /capabilities` | FR-019 | All | M0 (served) | Contract | TC-001 |
| OP-CAP-002 | GetProviderCapabilities | `GET /brokers/{brokerId}/capabilities` | FR-019 | All | M0 (served) | Contract | TC-001 |
| OP-BRK-001 | ListBrokers | `GET /brokers` | FR-012, FR-014 | All | M0 (served) | Contract | TC-001 |
| OP-BRK-002 | CreateBroker | `POST /brokers` | FR-012, FR-013 | Admin | M0 (served) | Contract, idempotency, integration (real broker) | TC-001, TC-002, TC-005, TC-030 |
| OP-BRK-003 | GetBroker | `GET /brokers/{brokerId}` | FR-012 | All | M0 (served) | Contract | TC-001 |
| OP-BRK-004 | UpdateBroker | `PATCH /brokers/{brokerId}` | FR-012 | Admin | M0 (served) | Contract, idempotency | TC-001, TC-002 |
| OP-BRK-005 | DeleteBroker | `DELETE /brokers/{brokerId}` | FR-012 | Admin | M0 (served) | Contract, idempotency, integration (real broker) | TC-001, TC-002, TC-005 |
| OP-BRK-006 | RunPreflight | `POST /brokers/{brokerId}/preflight` | FR-013 | Admin | M0 (served) | Contract, idempotency, integration (real broker) | TC-001, TC-005 |
| OP-QUE-001 | ListQueues | `GET /queues` | FR-001, FR-018 | All | M0 (served) | Contract, integration (real broker) | TC-001, TC-006 |
| OP-QUE-002 | GetQueue | `GET /queues/{queueId}` | FR-001, FR-003, FR-018 | All | M0 (served) | Contract, integration (real broker) | TC-001, TC-006, TC-019 |
| OP-QUE-003 | SetCapturePolicy | `PUT /queues/{queueId}/capture-policy` | FR-013, FR-015, FR-023 | Admin | M2 (deferred) | Contract, idempotency | TC-001, TC-002, TC-017 |
| OP-MSG-001 | SearchMessages | `QUERY /messages` | FR-006 | Support, Admin | M3 (deferred) | Contract, integration (real broker) | TC-001, TC-020 |
| OP-MSG-002 | GetMessage | `GET /messages/{messageId}` | FR-007 | Support, Admin | M3 (deferred) | Contract, integration (real broker) | TC-001, TC-020 |
| OP-MET-001 | GetMetricSummary | `GET /metrics/summary` | FR-004, FR-005 | All | M2 (deferred) | Contract, integration (real broker) | TC-001, TC-016, TC-019 |
| OP-MET-002 | GetMetricSeries | `GET /metrics/timeseries` | FR-004, FR-020 | All | M2 (deferred) | Contract, integration (real broker) | TC-001, TC-016 |
| OP-MET-003 | GetMetricAvailability | `GET /metrics/availability` | FR-021 | All | M2 (deferred) | Contract, integration (real broker) | TC-001, TC-016 |
| OP-OPR-001 | PeekQueue | `POST /operations/peek` | FR-008 | Support, Admin | M1 (served) | Contract, idempotency, integration (real broker) | TC-001, TC-002, TC-008, TC-010 |
| OP-OPR-002 | DiscardMessages | `POST /operations/discard` | FR-009, FR-028 | Support, Admin | M1 (served) | Contract, idempotency, dry run, durable-intent/reconciliation integration | TC-001, TC-002, TC-008, TC-028 |
| OP-OPR-003 | PurgeQueue | `POST /operations/purge` | FR-009 | Admin | M1 (served) | Contract, idempotency, dry run, integration (real broker) | TC-001, TC-002, TC-008 |
| OP-OPR-004 | ReplayMessages | `POST /operations/replay` | FR-010 | Support, Admin | M1 (served) | Contract, idempotency, dry run, integration (real broker) | TC-001, TC-002, TC-009 |
| OP-OPR-005 | PublishMessage | `POST /operations/publish` | FR-011 | Support, Admin | M3 (deferred) | Contract, idempotency, integration (real broker) | TC-001, TC-002, TC-021 |
| OP-OPR-006 | GetOperation | `GET /operations/{operationId}` | FR-010, FR-011 | Support, Admin | M1 (deferred) | Contract | TC-001, TC-009 |
| OP-OPR-007 | CreateProposal | `POST /operations/proposals` | FR-024 | Service principals | M3 (deferred) | Contract, idempotency | TC-001, TC-024 |
| OP-AUD-001 | ListAudit | `GET /audit` | FR-016 | Support, Admin | M1 (deferred) | Contract, integration (real broker) | TC-001, TC-008, TC-009 |
| OP-AUD-002 | ListAuditPayloads | `GET /audit/{auditId}/payloads` | FR-027 | Support, Admin | M1 (deferred) | Contract, integration (real broker) | TC-001, TC-027 |
| OP-AUTH-001 | SignInWithGoogle | `POST /auth/google` | FR-017 | Anyone with a Google account | M0 (served) | Contract, integration (real identity provider), unmapped-account refusal | TC-001, TC-031 |
| OP-AUTH-002 | GetSession | `GET /auth/session` | FR-017 | Any signed-in user | M0 (served) | Contract, session expiry | TC-001, TC-031 |
| OP-AUTH-003 | SignOut | `POST /auth/logout` | FR-017 | Any signed-in user | M0 (served) | Contract, revocation, CSRF | TC-001, TC-031 |

## Gaps

- **Provider-capability refusal is deferred to M5.** `ERR-CAP-001` is specified for a request the provider cannot perform. No v1 provider currently lacks an operation, so verification against a real adapter is scheduled with the second adapter; the branch remains covered by the documented error contract until then.
- **`GetOperation` is tied to FR-010 and FR-011** because replay and publish return an operation record; no requirement names it directly.
- **Generated-client compatibility (FR-019) is a planned build assertion**, to be exercised when a client generator and frontend build exist. TC-001 also checks that the promoted QUERY request survives generation; neither check has run.
- **`ListBrokers` carries FR-014** although multi-cluster grouping only becomes testable at M4. The operation is served at M0; the requirement is met later. That gap is deliberate and is why FR-014's criterion is M4 in [requirements §3.1](../02-business/requirements.md).
- **`ReplayMessages` shows one milestone for two deliveries.** The catalog's scope-by-revision table ships live redrive at M1 and adds captured-message replay at M3; this matrix records the first delivery only. The M3 expansion is covered by TC-020's capture-path integration and by FR-010, but it has no row of its own here.

The Codex documentation review on 2026-11-11 confirmed all operations then present have a requirement, role, milestone, and planned verification case; the three sign-in operations added in revision 0.4 follow the same rule. The deferred capability branch and milestone timing above are documented scope boundaries, not missing mappings. No case has been executed.
