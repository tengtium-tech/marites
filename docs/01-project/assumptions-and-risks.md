# Assumptions and risks

An assumption (ASM) is something the design relies on and has not proven. A risk (RSK) is something that could invalidate the plan, with its mitigation. Identifiers are stable and are never reused. The status words are those of the [documentation standard](documentation-standard.md).

## Assumptions

| ID | Assumption | Owner | Status |
| --- | --- | --- | --- |
| ASM-001 | The adopter already runs a configured broker; Marites never provisions or repairs one | [ADR-001](../05-architecture/decisions.md), DOC-SRS-001 C-003 | Accepted |
| ASM-002 | The broker user holds `configure`, `write` and `read` on `^marites\.` | DEC-003; [deployment §1.2](../11-operations/deployment-and-onboarding.md) | Accepted; a managed broker may refuse it (RSK-001) |
| ASM-003 | Adopter clusters may be independent rather than federated; per-cluster scoping holds either way | DEC-008 | Unverified; one plugin-list command settles it |
| ASM-004 | Traffic volume and payload size are unknown and are handled by per-queue settings and an enforced ceiling | DEC-005 | Accepted |
| ASM-005 | Producers remove sensitive values before publishing; Marites stores bodies verbatim | DEC-006 | Accepted |
| ASM-006 | Entra ID is available for identity, or the break-glass account and `MARITES_AUTH_MODE=none` cover its absence | ADR-008 | Accepted |
| ASM-007 | A get-and-requeue peek on a quorum queue with a delivery limit may count toward that limit | [architecture overview §2](../05-architecture/architecture-overview.md) | **Falsified at M1.** The message is destroyed and does not reach the dead-letter exchange; peek is refused on quorum queues |
| ASM-008 | One PostgreSQL instance and one container serve the single-server profile at the target rate | NFR-006, NFR-001 | Unverified; load test at M2 |
| ASM-009 | A Service Bus or Pub/Sub identity can be granted subscription management scoped to the watched topics, rather than at project or namespace level | [provider prerequisites §1.2, §2.2](../11-operations/provider-prerequisites.md) | Unverified; no platform has been configured |
| ASM-010 | Compaction can be made idempotent per window without a schema change | [ADR-024](../05-architecture/decisions.md) | Confirmed by design: recomputation and assignment need no new table, and ASM-010's "no schema change" held |

## Risks

| ID | Risk | Mitigation | Owner |
| --- | --- | --- | --- |
| RSK-001 | A managed broker (for example Amazon MQ) blocks topology writes, disabling continuous capture | Preflight names the missing grant; the live console still works; capture is refused rather than degraded silently | DEC-003 |
| RSK-002 | The 5,000 msg/s target is unmeasured and the batch and prefetch sizing is untested | Load test at M2 with prefetch above the batch size | NFR-001; ADR-011 |
| RSK-003 | Three provider adapters do not fit one milestone | Delivery order RabbitMQ, then Service Bus, then Pub/Sub; M2 means RabbitMQ capture only | DEC-001 |
| RSK-004 | A bounded shadow queue drops copies silently while the worker is stalled | Compare observed counts with the broker's publish counter; raise the capture-gap warning | ADR-019; FR-018 |
| RSK-005 | A redelivery after a crash double-counts permanently, because rollups are never rebuilt | Redelivery ledger | ADR-019 |
| RSK-006 | Payloads containing PII or credentials are stored verbatim | State the position at onboarding; `payload_policy = none` still counts exactly | DEC-006 |
| RSK-007 | Capture on Service Bus or Pub/Sub roughly doubles the adopter's delivery operations and cost | Documented per provider; size before enabling capture on a firehose topic | [deployment §1.5](../11-operations/deployment-and-onboarding.md) |
| RSK-008 | The in-house dispatcher grows into the framework that was rejected | Keep it near fifty lines; cross-cutting concerns stay explicit decorators | ADR-013 |
| RSK-009 | A document is mistaken for evidence of implementation | The README and standard state that nothing is built or executed | [documentation standard](documentation-standard.md) |
| RSK-010 | A retried compaction over additive upserts doubles a window's counts permanently, and totals are never rebuilt from rows | **Closed.** Compaction recomputes the window's absolute totals from the finer tier and assigns them, so a retry writes the same numbers; the three orderings that make the recomputation correct are part of the decision (ADR-024) | [ADR-024](../05-architecture/decisions.md) |
| RSK-011 | Neither bookkeeping SQLite nor the batched message store uses database row-level security; tenant isolation depends on explicit repository/API query scope | Tenant scope is supplied by authenticated Application calls and included in every query; TC-022 asserts isolation. Moving message capture to RLS changes the write path | [migration plan §4](../07-data-and-contracts/migration-plan.md) |
| RSK-012 | Nothing alerts. A stopped capture is discovered by a person, and the dashboard may look quiet for hours | Runbook symptoms and `captureState`; the product gap is recorded rather than solved | [runbook §7](../11-operations/runbook.md) |
| RSK-013 | Spilled payload objects have no lifecycle, so deletion and backup scope are undefined | **Closed.** The bucket layout is content-addressed, the sweeper deletes an object when no capture row references its hash, the bucket is named in the backup scope, and a failed spill degrades to a recorded truncation rather than blocking capture | [data dictionary §6](../07-data-and-contracts/data-dictionary.md) |
| RSK-014 | Two accepted ADRs (011, 019) carry the "exact counts" promise and neither has an executed test | TC-013 and TC-014 are milestone gates, not optional tests | [verification plan §3](../10-testing/verification-plan.md) |

**Owners are decision identifiers, not people** — the project has one author and no external commitment, so a delivery constraint is recorded only when one exists. The risks that remain open are the ones a test can settle rather than a decision: RSK-002 and ASM-007 when their tests run, RSK-008 and RSK-011 when the code exists.

Use the [documentation structure](documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
