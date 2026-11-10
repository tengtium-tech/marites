# Readiness dashboard

Version 0.2 | November 2026 | Status: single summary of documentation readiness by layer; summarises each source document's own status and adds no new policy.

## How to use this page

This is the one place that summarises overall status. Update it in the same change that alters a status.

Nothing here reports an executed test, a built service or a verified deployment. Application code does not exist.

| Status | Meaning |
| --- | --- |
| Drafted | Written to the intended scope and awaiting review |
| Partial | Written, with named gaps that change behaviour |
| Not started | No substantive document exists |
| Planned | Verification specified and not executed |

## Readiness gate

M0 exits when the contract is reviewed, `/capabilities` and preflight are served, the setup wizard blocks a failing save, and `docker compose up` yields a working UI against a test broker ([DOC-OPS-001 §6](../11-operations/deployment-and-onboarding.md)). None of this exists. All nineteen decisions are answered and ADR-001 to ADR-024 stand (23 accepted, ADR-004 superseded), so no decision blocks M0.

## By layer

| Layer | Status | Source and what is missing |
| --- | --- | --- |
| Requirements | Drafted | [DOC-SRS-001](../02-business/requirements.md): 29 FR, 8 NFR, each with one acceptance criterion. No criterion has been exercised |
| Decisions | Drafted | [DEC-001 to DEC-019](../01-project/decisions-and-open-questions.md) answered; ADR-001 to ADR-024, 23 accepted (ADR-004 superseded). DEC-008's premise is unconfirmed |
| Workflows | Drafted | [Five records](../03-workflows/README.md): three lifecycles, failure paths, retention and compaction. No diagram carries a timing threshold, because none are set |
| Architecture | Drafted | [Overview](../05-architecture/architecture-overview.md), [C4](../05-architecture/c4-model.md), [failure modes](../05-architecture/failure-modes.md), [adapter design](../05-architecture/provider-adapters.md). Compaction idempotency is specified by ADR-024, not left open |
| Data model | Drafted | [Twelve tables in two stores](../07-data-and-contracts/data-model.md) — nine of Marites' own, three in the optional message store — including `peak_rate` and `capture_dedupe`. Column-level detail in the [dictionary](../07-data-and-contracts/data-dictionary.md) |
| Migration | Planned | [First migration plan](../07-data-and-contracts/migration-plan.md): order, partitioning, row-level security, rollback. Nothing applied |
| API contract | Partial | [Served](../07-data-and-contracts/openapi/openapi.yaml) (10) and [deferred](../07-data-and-contracts/openapi/openapi.deferred.yaml) (15) both validate as OpenAPI 3.1. No conformance suite, no generated client |
| Error catalog | Drafted | [26 codes](../07-data-and-contracts/error-catalog.md), statuses aligned to the conventions |
| Experience | Drafted | [Metric rules](../08-experience/dashboard-and-operations.md), [wireframes and navigation](../08-experience/wireframes-and-navigation.md), [accessibility](../08-experience/accessibility.md), [visual design](../08-experience/visual-design.md). No responsive layout |
| Security | Drafted | [Threat model](../09-security/threat-model.md): 26 threats, 7 with a planned case, six stated residuals. Key rotation is a procedure ([runbook §8](../11-operations/runbook.md)). No penetration test |
| Platform foundation | Drafted | [Index](../06-platform-foundation/README.md), [configuration reference](../11-operations/configuration-reference.md) (22 variables), [health and metrics](../11-operations/deployment-and-onboarding.md) §3.2. No alerting by decision (RSK-012) |
| Operations | Drafted | [Deployment](../11-operations/deployment-and-onboarding.md), [provider prerequisites](../11-operations/provider-prerequisites.md), [upgrade and rollback](../11-operations/upgrade-and-rollback.md), [backup and recovery](../11-operations/backup-and-recovery.md), [runbook](../11-operations/runbook.md). No alerting |
| Testing | Planned | [Verification plan](../10-testing/verification-plan.md): 7 levels, 29 cases, 8 gates. Nothing written, nothing executed |
| Traceability | Partial | [Operation matrix](../13-traceability/operation-coverage-matrix.md) carries levels and cases; requirement crosswalk in [requirements §7](../02-business/requirements.md) |
| Vocabulary | Drafted | [Controlled values](../14-reference/vocabulary.md), including `captureState` (§15), added November 2026 |
| Application code and tests | Not started | Nothing implemented or executed |

## The four things that would change this page most

| Item | Why it matters | Where it is recorded |
| --- | --- | --- |
| Compaction idempotency | A retried compaction over additive upserts doubles a total permanently, and totals are never rebuilt | [Failure modes §6](../05-architecture/failure-modes.md) |
| Capture-table isolation | Row-level security covers configuration but not capture, because the write path batches across tenants | [Migration plan §4](../07-data-and-contracts/migration-plan.md) |
| Alerting and health | Every runbook symptom is discovered by a person; there is no way to watch Marites with Marites | [Runbook §7](../11-operations/runbook.md) |
| Executed evidence | Two accepted ADRs (011, 019) carry the counting promise and neither has a test | [Verification plan §3](../10-testing/verification-plan.md) |

## What this page is not

It is not a percentage, and it is not a burn-down. A layer marked Drafted means a document exists and has been read by its author; it says nothing about whether the design is right. The three layers marked Partial are the honest ones: each has a named gap that would change behaviour if it were resolved differently.
