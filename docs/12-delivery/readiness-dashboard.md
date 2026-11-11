# Readiness dashboard

Version 0.4 | November 2026 | Status: Approved — Product owner, 2026-11-11

## How to use this page

This is the one place that summarises overall status. Update it in the same change that alters a status.

Nothing here reports an executed test, a built service or a verified deployment. Production application code does not exist; the UI screenshots are design references only.

| Status | Meaning |
| --- | --- |
| Drafted | Written to the intended scope and awaiting review |
| Partial | Written, with named gaps that change behaviour |
| Reviewed | A named documentation review found no open finding at the stated scope; execution evidence is separate |
| Approved | The document owner accepts a Reviewed baseline; approver and date are recorded in document control |
| Not started | No substantive document exists |
| Planned | Verification specified and not executed |

## Readiness gate

M0 exits when the contract is reviewed, `/capabilities` and preflight are served, the setup wizard blocks a failing save, and `docker compose up` yields a working UI against a test broker ([DOC-OPS-001 §6](../11-operations/deployment-and-onboarding.md)). None of this is implemented. All nineteen decisions are answered and ADR-001 to ADR-025 stand (23 accepted, ADR-004 and ADR-005 superseded), so no unresolved product decision blocks M0 planning.

## By layer

| Layer | Documentation status | Implementation / verification status | Source and remaining work |
| --- | --- | --- | --- |
| Requirements | Approved | Planned; no criterion has been exercised | [DOC-SRS-001](../02-business/requirements.md): 29 FR and eight NFR, each with one acceptance criterion |
| Decisions | Approved | No formal decision is open; DEC-008's premise remains an assumption to confirm in the adopter environment | [DEC-001 to DEC-019](../01-project/decisions-and-open-questions.md); ADR-001 to ADR-025, 23 accepted and two superseded |
| Workflows | Approved | No application execution; timing values are only those recorded in configuration and milestones | [Five records](../03-workflows/README.md): three lifecycles, failure paths, retention and compaction |
| Architecture | Approved | Implementation not started | [Overview](../05-architecture/architecture-overview.md), [C4](../05-architecture/c4-model.md), [failure modes](../05-architecture/failure-modes.md), and [adapter design](../05-architecture/provider-adapters.md) |
| Data model | Approved | No schema applied | [Twelve tables in two stores](../07-data-and-contracts/data-model.md) — nine bookkeeping and three optional message-store tables — including `peak_rate` and `capture_dedupe`; column detail in the [dictionary](../07-data-and-contracts/data-dictionary.md) |
| Migration | Approved | Planned; nothing applied | [First migration plan](../07-data-and-contracts/migration-plan.md): separate SQLite/PostgreSQL streams, partitioning, tenant isolation, rollback, and named operational gaps |
| API contract | Approved | Both contracts pass static lint; service and generated-client conformance remain Planned in TC-001 | [Served](../07-data-and-contracts/openapi/openapi.yaml) (10) and [deferred](../07-data-and-contracts/openapi/openapi.deferred.yaml) (15), OpenAPI 3.2.1 |
| Error catalog | Approved | Implementation not started | [27 codes](../07-data-and-contracts/error-catalog.md), aligned with contract conventions |
| Experience | Approved | UI implementation not started; workstation layout is the target, narrow reflow is covered by NFR-008, phone layout is out of scope | [Metric rules](../08-experience/dashboard-and-operations.md), [wireframes](../08-experience/wireframes-and-navigation.md), [accessibility](../08-experience/accessibility.md), and [visual design](../08-experience/visual-design.md) |
| Security | Approved | No controls implemented or tested; no penetration test | [Threat model](../09-security/threat-model.md): 26 threats, six planned cases cover ten threats, and six residual risks are stated |
| Platform foundation | Approved | No alerting by accepted scope decision RSK-012 | [Index](../06-platform-foundation/README.md), [configuration reference](../11-operations/configuration-reference.md) (24 variables), and [health/metrics](../11-operations/deployment-and-onboarding.md) §3.2 |
| Operations | Approved | Procedures have not been exercised; alerting is out of scope | [Deployment](../11-operations/deployment-and-onboarding.md), [provider prerequisites](../11-operations/provider-prerequisites.md), [upgrade/rollback](../11-operations/upgrade-and-rollback.md), [backup/recovery](../11-operations/backup-and-recovery.md), and [runbook](../11-operations/runbook.md) |
| Testing | Approved | Planned; seven levels, 29 cases, eight gates; no test code or execution evidence | [Verification plan](../10-testing/verification-plan.md) |
| Traceability | Approved | Planned cases are not executed | [Requirement crosswalk](../02-business/requirements.md) covers 29 FR and eight NFR; the [operation matrix](../13-traceability/operation-coverage-matrix.md) maps all 25 operations |
| Vocabulary | Approved | Controlled source | [Controlled values](../14-reference/vocabulary.md), including `captureState` (§15) |
| Application code and tests | Not started | Nothing implemented or executed | No production source, tests or build exists |

The Product Owner approved the documentation baseline on 2026-11-11. Approval accepts the described scope, assumptions, deferred items, and risks; it does not claim that implementation, migration, or application verification has occurred.

## The four things that would change this page most

| Item | Why it matters | Where it is recorded |
| --- | --- | --- |
| Compaction idempotency | A retried compaction over additive upserts doubles a total permanently, and totals are never rebuilt | [Failure modes §6](../05-architecture/failure-modes.md) |
| Tenant isolation | Both stores depend on explicit tenant scoping; the batched message store has no RLS | [Migration plan §4](../07-data-and-contracts/migration-plan.md) |
| Alerting and health | Every runbook symptom is discovered by a person; there is no way to watch Marites with Marites | [Runbook §7](../11-operations/runbook.md) |
| Executed evidence | Two accepted ADRs (011, 019) carry the counting promise and neither has a test | [Verification plan §3](../10-testing/verification-plan.md) |

## What this page is not

It is not a percentage, and it is not a burn-down. `Reviewed` records a documentation review at the stated scope; it does not imply that production code, generated clients, migrations, or tests exist. These remain Not started or Planned until executable evidence is produced.
