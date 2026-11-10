# Readiness dashboard

Version 0.2 | 09 November 2026 | Status: single summary of documentation readiness by layer; summarises each source document's own status and adds no new policy.

## How to use this page

This is the one place that summarises overall status. The [documentation review](documentation-review.md) keeps its own scope and links here rather than restating totals. Update this page in the same change that alters a status.

Nothing here reports an executed test, a built service or a verified deployment. Application code does not exist.

| Status | Meaning |
| --- | --- |
| Drafted | Written to the intended scope and awaiting review |
| Partial | Written, with named gaps that change behaviour |
| Not started | No substantive document exists |
| Planned | Verification specified and not executed |

## Readiness gate

M0 exits when the contract is reviewed, `/capabilities` and preflight are served, the setup wizard blocks a failing save, and `docker compose up` yields a working UI against a test broker ([DOC-OPS-001 §6](../operations/deployment-and-onboarding.md)). None of this exists. All nineteen decisions are answered and ADR-001 to ADR-023 stand (22 accepted, ADR-004 superseded), so no decision blocks M0.

## By layer

| Layer | Status | Source and what is missing |
| --- | --- | --- |
| Requirements | Drafted | [DOC-SRS-001](../business/requirements.md): 28 FR, 8 NFR, each with one acceptance criterion. No criterion has been exercised |
| Decisions | Drafted | [DEC-001 to DEC-019](../project/decisions-and-open-questions.md) answered; ADR-001 to ADR-023, 22 accepted (ADR-004 superseded). DEC-008's premise is unconfirmed |
| Workflows | Drafted | [Five records](../workflows/README.md): three lifecycles, failure paths, retention and compaction. No diagram carries a timing threshold, because none are set |
| Architecture | Partial | [Overview](../architecture/architecture-overview.md), [C4](../architecture/c4-model.md), [failure modes](../architecture/failure-modes.md), [adapter design](../architecture/provider-adapters.md). **Compaction idempotency is unspecified** |
| Data model | Drafted | [Twelve tables in two stores](../data-and-contracts/data-model.md) — nine of Marites' own, three in the optional message store — including `peak_rate` and `capture_dedupe`. Column-level detail in the [dictionary](../data-and-contracts/data-dictionary.md) |
| Migration | Planned | [First migration plan](../data-and-contracts/migration-plan.md): order, partitioning, row-level security, rollback. Nothing applied |
| API contract | Partial | [Served](../data-and-contracts/openapi/openapi.yaml) (10) and [deferred](../data-and-contracts/openapi/openapi.deferred.yaml) (13) both validate as OpenAPI 3.1. No conformance suite, no generated client |
| Error catalog | Drafted | [26 codes](../data-and-contracts/error-catalog.md), statuses aligned to the conventions |
| Experience | Drafted | [Metric rules](../experience/dashboard-and-operations.md), [wireframes and navigation](../experience/wireframes-and-navigation.md), [accessibility](../experience/accessibility.md), [visual design](../experience/visual-design.md). No responsive layout |
| Security | Partial | [Threat model](../security/threat-model.md): 26 threats, 7 with a planned case. **No key-rotation procedure; no penetration test** |
| Platform foundation | Partial | [Index](../platform-foundation/README.md). Observability, configuration reference and health endpoints unspecified |
| Operations | Drafted | [Deployment](../operations/deployment-and-onboarding.md), [provider prerequisites](../operations/provider-prerequisites.md), [upgrade and rollback](../operations/upgrade-and-rollback.md), [backup and recovery](../operations/backup-and-recovery.md), [runbook](../operations/runbook.md). No alerting |
| Testing | Planned | [Verification plan](../testing/verification-plan.md): 7 levels, 28 cases, 8 gates. Nothing written, nothing executed |
| Traceability | Partial | [Operation matrix](../traceability/operation-coverage-matrix.md) carries levels and cases; requirement crosswalk in [requirements §7](../business/requirements.md) |
| Vocabulary | Drafted | [Controlled values](../reference/vocabulary.md), including `captureState` (§15), added 09 November 2026 |
| Application code and tests | Not started | Nothing implemented or executed |

## The four things that would change this page most

| Item | Why it matters | Where it is recorded |
| --- | --- | --- |
| Compaction idempotency | A retried compaction over additive upserts doubles a total permanently, and totals are never rebuilt | [Failure modes §6](../architecture/failure-modes.md) |
| Capture-table isolation | Row-level security covers configuration but not capture, because the write path batches across tenants | [Migration plan §4](../data-and-contracts/migration-plan.md) |
| Alerting and health | Every runbook symptom is discovered by a person; there is no way to watch Marites with Marites | [Runbook §7](../operations/runbook.md) |
| Executed evidence | Two accepted ADRs (011, 019) carry the counting promise and neither has a test | [Verification plan §3](../testing/verification-plan.md) |

## What this page is not

It is not a percentage, and it is not a burn-down. A layer marked Drafted means a document exists and has been read by its author; it says nothing about whether the design is right. The three layers marked Partial are the honest ones: each has a named gap that would change behaviour if it were resolved differently.
