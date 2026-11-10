# Assumptions and risks

Version 0.2 | 09 November 2026 | Status: Drafted; every item restates a fact recorded in its owning document and adds no new policy.

An assumption (ASM) is something the design relies on and has not proven. A risk (RSK) is something that could invalidate the plan, with its mitigation. Identifiers are stable and are never reused. The status words are those of the [documentation standard](documentation-standard.md).

## Assumptions

| ID | Assumption | Owner | Status |
| --- | --- | --- | --- |
| ASM-001 | The adopter already runs a configured broker; Marites never provisions or repairs one | [ADR-001](../architecture/decisions.md), DOC-SRS-001 C-003 | Accepted |
| ASM-002 | The broker user holds `configure`, `write` and `read` on `^marites\.` | DEC-003; [deployment §1.2](../operations/deployment-and-onboarding.md) | Accepted; a managed broker may refuse it (RSK-001) |
| ASM-003 | Adopter clusters may be independent rather than federated; per-cluster scoping holds either way | DEC-008 | Unverified; one plugin-list command settles it |
| ASM-004 | Traffic volume and payload size are unknown and are handled by per-queue settings and an enforced ceiling | DEC-005 | Accepted |
| ASM-005 | Producers remove sensitive values before publishing; Marites stores bodies verbatim | DEC-006 | Accepted |
| ASM-006 | Entra ID is available for identity, or the break-glass account and `MARITES_AUTH_MODE=none` cover its absence | ADR-008 | Accepted |
| ASM-007 | A get-and-requeue peek on a quorum queue with a delivery limit may count toward that limit | [architecture overview §2](../architecture/architecture-overview.md) | Unverified; tested at M1 |
| ASM-008 | One PostgreSQL instance and one container serve the single-server profile at the target rate | NFR-006, NFR-001 | Unverified; load test at M2 |
| ASM-009 | A Service Bus or Pub/Sub identity can be granted subscription management scoped to the watched topics, rather than at project or namespace level | [provider prerequisites §1.2, §2.2](../operations/provider-prerequisites.md) | Unverified; no platform has been configured |
| ASM-010 | Compaction can be made idempotent per window without a schema change | [failure modes §6](../architecture/failure-modes.md) | Unverified, and the guard is not specified (RSK-010) |

## Risks

| ID | Risk | Mitigation | Owner |
| --- | --- | --- | --- |
| RSK-001 | A managed broker (for example Amazon MQ) blocks topology writes, disabling continuous capture | Preflight names the missing grant; the live console still works; capture is refused rather than degraded silently | DEC-003 |
| RSK-002 | The 5,000 msg/s target is unmeasured and the batch and prefetch sizing is untested | Load test at M2 with prefetch above the batch size | NFR-001; ADR-011 |
| RSK-003 | Three provider adapters do not fit one milestone | Delivery order RabbitMQ, then Service Bus, then Pub/Sub; M2 means RabbitMQ capture only | DEC-001 |
| RSK-004 | A bounded shadow queue drops copies silently while the worker is stalled | Compare observed counts with the broker's publish counter; raise the capture-gap warning | ADR-019; FR-018 |
| RSK-005 | A redelivery after a crash double-counts permanently, because rollups are never rebuilt | Redelivery ledger | ADR-019 |
| RSK-006 | Payloads containing PII or credentials are stored verbatim | State the position at onboarding; `payload_policy = none` still counts exactly | DEC-006 |
| RSK-007 | Capture on Service Bus or Pub/Sub roughly doubles the adopter's delivery operations and cost | Documented per provider; size before enabling capture on a firehose topic | [deployment §1.5](../operations/deployment-and-onboarding.md) |
| RSK-008 | The in-house dispatcher grows into the framework that was rejected | Keep it near fifty lines; cross-cutting concerns stay explicit decorators | ADR-013 |
| RSK-009 | A document is mistaken for evidence of implementation | The README and standard state that nothing is built or executed | [documentation standard](documentation-standard.md) |
| RSK-010 | A retried compaction over additive upserts doubles a window's counts permanently, and totals are never rebuilt from rows | **No guard is specified.** The interim control is operational: stop compaction and preserve both tiers as evidence | [failure modes §6](../architecture/failure-modes.md) |
| RSK-011 | Row-level security covers configuration tables but not the capture tables, so isolation depends on query shape | Tenant-scoped keys and API query shape; TC-022 asserts it. Moving capture to RLS changes the write path | [migration plan §4](../data-and-contracts/migration-plan.md) |
| RSK-012 | Nothing alerts. A stopped capture is discovered by a person, and the dashboard may look quiet for hours | Runbook symptoms and `captureState`; the product gap is recorded rather than solved | [runbook §7](../operations/runbook.md) |
| RSK-013 | Spilled payload objects have no lifecycle, so deletion and backup scope are undefined | Recorded as a gap in the migration plan and the backup procedure | [data dictionary §6](../data-and-contracts/data-dictionary.md) |
| RSK-014 | Two accepted ADRs (011, 019) carry the "exact counts" promise and neither has an executed test | TC-013 and TC-014 are milestone gates, not optional tests | [verification plan §3](../testing/verification-plan.md) |

## Work still needed

Owners are decision identifiers, not people: the project has one author and no external commitment. Add a delivery constraint only when one exists. Review RSK-002 and ASM-007 when their tests run; review RSK-010 and ASM-010 when the compaction guard is specified.

Use the [documentation structure](documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
