# Marites documentation

Version 0.10 | 10 November 2026 | Status: specification complete for the planned system; no code exists

Marites observes message traffic crossing an adopter-supplied messaging platform — RabbitMQ, Azure Service Bus or Google Pub/Sub — and lets authorised operators count it, inspect it, and recover from failures. It is a **client** of a broker the adopter already runs: it never provisions, hosts, upgrades or repairs one.

The scope is settled. All nineteen recorded decisions are answered, and nothing blocks M0. Every requirement now carries an acceptance criterion, and every criterion cites a planned case — none of which has been executed.

## Reading order

The documentation uses named sections without numeric prefixes. Each document has one authoritative location. The [documentation structure](project/documentation-structure.md) defines placement and maintenance; the [area catalog](domains/domain-catalog.md) maps responsibilities without selecting deployable boundaries. Each section index states what exists and what is still missing. A folder is not a completed specification.

| Section | Contents |
| --- | --- |
| [Project](project/README.md) | The [portfolio overview](project/portfolio-overview.md), decisions, assumptions and risks, the documentation standard, structure and changelog. Read first |
| [Business](business/README.md) | Requirements with acceptance criteria, actors, constraints and scope |
| [Workflows](workflows/README.md) | The capture, recovery and onboarding lifecycles, failure paths, and the retention and compaction lifecycle |
| [Domains](domains/README.md) | Five documentation areas with responsibilities, records and handoffs |
| [Architecture](architecture/README.md) | The one-hop tap, module map, capability model, component failure modes, adapter design and every accepted decision |
| [Platform foundation](platform-foundation/README.md) | Reliable capture, identity, audit, configuration and process topology (index) |
| [Data and contracts](data-and-contracts/README.md) | Tables, the data dictionary, the migration plan, counting semantics, the HTTP contract and error codes |
| [Experience](experience/README.md) | Metric presentation, role capabilities, wireframes and navigation, accessibility |
| [Security](security/README.md) | The [threat model](security/threat-model.md), credentials, payload responsibility and operation safeguards |
| [Testing](testing/README.md) | The [verification plan](testing/verification-plan.md): levels, 28 cases, eight gates; nothing executed |
| [Operations](operations/README.md) | Adopter prerequisites for all three providers, deployment, upgrade, backup, runbook and milestones |
| [Delivery](delivery/README.md) | The [readiness dashboard](delivery/readiness-dashboard.md) and the [documentation review](delivery/documentation-review.md) |
| [Traceability](traceability/README.md) | The operation-to-requirement matrix and the verification crosswalks |
| [Reference](reference/README.md) | The controlled vocabulary |

For a first read: [requirements](business/requirements.md) §2, then the [three lifecycles](workflows/README.md), then [architecture overview](architecture/architecture-overview.md) §2 and §3, then the [data model](data-and-contracts/data-model.md) §3, then the [served contract](data-and-contracts/openapi/openapi.yaml).

## Current status

| Artifact | Status |
| --- | --- |
| Shared section structure | Fourteen named sections; one is index-only by design. See the [structure guide](project/documentation-structure.md) |
| Requirements and decisions | Drafted. 28 FR, 8 NFR, each with one acceptance criterion; 19 DEC answered, 23 ADRs (22 accepted, ADR-004 superseded) |
| Architecture and data model | Drafted. Twelve tables in two stores, twelve source projects, four ports, nine components with failure modes |
| API contract | 10 operations served and 15 deferred; both validate as OpenAPI 3.1 |
| Verification | Planned only: 28 cases, 8 gates, 0 executed — [verification plan](testing/verification-plan.md) |
| Security | Partial. 26 threats, 7 with a planned case; no key-rotation procedure, no penetration test |
| Review | Three review passes, 69 findings: 68 resolved, 1 withdrawn — [documentation review](delivery/documentation-review.md) |
| Application code and tests | Not started |

## Authority and interpretation

When two documents disagree, this table decides which one wins.

| Concept | Owner |
|---|---|
| Requirements, priorities, scope, acceptance criteria | [business/requirements.md](business/requirements.md) |
| Canonical enumerated values, roles, capabilities, metric names, identifiers | [reference/vocabulary.md](reference/vocabulary.md) |
| Settled architecture decisions | [architecture/decisions.md](architecture/decisions.md) |
| Open questions and their status | [project/decisions-and-open-questions.md](project/decisions-and-open-questions.md) |
| Broker prerequisites and adopter obligations | [operations/deployment-and-onboarding.md](operations/deployment-and-onboarding.md) §1; [provider-prerequisites.md](operations/provider-prerequisites.md) for Service Bus and Pub/Sub |
| Table structures and relationships | [data-and-contracts/data-model.md](data-and-contracts/data-model.md) |
| Column types, nullability, defaults and constraints | [data-and-contracts/data-dictionary.md](data-and-contracts/data-dictionary.md) |
| Counting semantics and metric definitions | [data-and-contracts/data-model.md](data-and-contracts/data-model.md) §3; [experience/dashboard-and-operations.md](experience/dashboard-and-operations.md) §1 |
| HTTP rules — outcomes, idempotency, paging, versioning | [data-and-contracts/contract-conventions.md](data-and-contracts/contract-conventions.md) |
| Error codes | [data-and-contracts/error-catalog.md](data-and-contracts/error-catalog.md) |
| The API surface itself | [data-and-contracts/openapi/openapi.yaml](data-and-contracts/openapi/openapi.yaml) |
| Repository organisation | [architecture/architecture-overview.md](architecture/architecture-overview.md) §6 |
| Component failure behaviour | [architecture/failure-modes.md](architecture/failure-modes.md) |
| Verification levels, cases and gates | [testing/verification-plan.md](testing/verification-plan.md) |
| How test traffic is produced and seeded | [testing/traffic-harness.md](testing/traffic-harness.md) |
| Every configuration variable, in one table | [operations/configuration-reference.md](operations/configuration-reference.md) |
| What the work is, and what blocks it | [delivery/delivery-plan.md](delivery/delivery-plan.md) |
| Every screen and dialog, as an image | [experience/screenshot-index.md](experience/screenshot-index.md) |
| Threats and their controls | [security/threat-model.md](security/threat-model.md) |
| Screen layout and navigation | [experience/wireframes-and-navigation.md](experience/wireframes-and-navigation.md) |
| Accessibility requirements | [experience/accessibility.md](experience/accessibility.md) |
| Identifiers and readiness vocabulary | [project/documentation-standard.md](project/documentation-standard.md) |
| Assumptions and risks | [project/assumptions-and-risks.md](project/assumptions-and-risks.md) |
| Overall readiness | [delivery/readiness-dashboard.md](delivery/readiness-dashboard.md) |
| Operation-to-requirement mapping | [traceability/operation-coverage-matrix.md](traceability/operation-coverage-matrix.md) |
| Verification | **Nothing yet.** No code, no tests, no execution evidence. |

**A document is not evidence of implementation.** Nothing here has been built, run, or verified against a broker. Statements about behaviour describe intent.

**Paths in prose are relative to this `docs/` directory.** The contract lives inside the documentation because it is the product boundary, not a build artefact.

## Document register

Forty-eight documents, fourteen section indexes, one contract-folder index and this page. Section indexes are listed in the reading-order table above and are not repeated here.

| Document | Version | Status |
| --- | --- | --- |
| [project/portfolio-overview.md](project/portfolio-overview.md) | 0.1 | Current — what this set demonstrates, and what it does not |
| [project/documentation-structure.md](project/documentation-structure.md) | 0.1 | Adopted — the fourteen sections |
| [project/documentation-standard.md](project/documentation-standard.md) | 0.1 | Adopted — how this set is maintained |
| [project/decisions-and-open-questions.md](project/decisions-and-open-questions.md) | 0.1 | 17 questions, all answered |
| [project/assumptions-and-risks.md](project/assumptions-and-risks.md) | 0.2 | Drafted — 10 assumptions, 14 risks |
| [project/documentation-changelog.md](project/documentation-changelog.md) | 0.2 | Baselines 0.1 to 0.5 |
| [project/plan-v0.1-superseded.md](project/plan-v0.1-superseded.md) | 0.1 | Superseded |
| [business/requirements.md](business/requirements.md) | 0.1 | Draft; 28 FR and 8 NFR with acceptance criteria |
| [workflows/capture-lifecycle.md](workflows/capture-lifecycle.md) | 0.1 | Draft |
| [workflows/recovery-lifecycle.md](workflows/recovery-lifecycle.md) | 0.1 | Draft |
| [workflows/onboarding-lifecycle.md](workflows/onboarding-lifecycle.md) | 0.1 | Draft |
| [workflows/failure-paths.md](workflows/failure-paths.md) | 0.1 | Draft |
| [workflows/retention-lifecycle.md](workflows/retention-lifecycle.md) | 0.1 | Draft |
| [domains/domain-catalog.md](domains/domain-catalog.md) | 0.1 | Drafted — five documentation areas |
| [architecture/c4-model.md](architecture/c4-model.md) | 0.1 | Draft; context, containers, components |
| [architecture/architecture-overview.md](architecture/architecture-overview.md) | 0.1 | Draft |
| [architecture/failure-modes.md](architecture/failure-modes.md) | 0.1 | Draft; nine components |
| [architecture/provider-adapters.md](architecture/provider-adapters.md) | 0.1 | Draft; three adapters |
| [architecture/mcp-integration.md](architecture/mcp-integration.md) | 0.1 | Draft; agent surface and approval model |
| [architecture/decisions.md](architecture/decisions.md) | 0.1 | 22 accepted, 1 superseded |
| [data-and-contracts/data-model.md](data-and-contracts/data-model.md) | 0.1 | Draft |
| [data-and-contracts/data-dictionary.md](data-and-contracts/data-dictionary.md) | 0.1 | Draft; twelve tables, column level |
| [data-and-contracts/migration-plan.md](data-and-contracts/migration-plan.md) | 0.1 | Planned — nothing applied |
| [data-and-contracts/contract-conventions.md](data-and-contracts/contract-conventions.md) | 0.1 | Draft; binding on the contract |
| [data-and-contracts/api-catalog.md](data-and-contracts/api-catalog.md) | 0.2 | 25 operations — 10 served, 15 deferred |
| [data-and-contracts/error-catalog.md](data-and-contracts/error-catalog.md) | 0.1 | 26 codes |
| [data-and-contracts/openapi/openapi.yaml](data-and-contracts/openapi/openapi.yaml) | 0.2.0 | Served contract — 10 operations |
| [data-and-contracts/openapi/openapi.deferred.yaml](data-and-contracts/openapi/openapi.deferred.yaml) | 0.2.0 | Holding area — 15 operations |
| [experience/dashboard-and-operations.md](experience/dashboard-and-operations.md) | 0.1 | Draft |
| [experience/wireframes-and-navigation.md](experience/wireframes-and-navigation.md) | 0.1 | Draft; layout in text |
| [experience/accessibility.md](experience/accessibility.md) | 0.1 | Draft; NFR-008 in full |
| [experience/visual-design.md](experience/visual-design.md) | 0.1 | Draft; tokens and contrast, measured |
| [security/threat-model.md](security/threat-model.md) | 0.1 | Draft; 26 threats |
| [testing/verification-plan.md](testing/verification-plan.md) | 0.1 | Planned; 28 cases, 0 executed |
| [testing/traffic-harness.md](testing/traffic-harness.md) | 0.1 | Specified; producer, rejecting consumer, nine scenarios, none built |
| [operations/configuration-reference.md](operations/configuration-reference.md) | 0.1 | Specified; 14 variables, none read by a binary yet |
| [delivery/delivery-plan.md](delivery/delivery-plan.md) | 0.1 | Specified; 8 milestones, 30 packages, nothing scheduled |
| [delivery/ci-and-release.md](delivery/ci-and-release.md) | 0.1 | Specified; ten gates, no pipeline exists |
| [delivery/github-project.md](delivery/github-project.md) | 0.1 | Specified; no repository exists yet |
| [experience/screenshot-index.md](experience/screenshot-index.md) | 0.1 | 35 images from one build; capture script committed |
| [operations/deployment-and-onboarding.md](operations/deployment-and-onboarding.md) | 0.1 | Draft |
| [operations/provider-prerequisites.md](operations/provider-prerequisites.md) | 0.1 | Draft; Service Bus and Pub/Sub |
| [operations/storage-prerequisites.md](operations/storage-prerequisites.md) | 0.1 | Draft; PostgreSQL and SQL Server grants |
| [operations/upgrade-and-rollback.md](operations/upgrade-and-rollback.md) | 0.1 | Planned; no release exists |
| [operations/backup-and-recovery.md](operations/backup-and-recovery.md) | 0.1 | Planned; no restore tested |
| [operations/runbook.md](operations/runbook.md) | 0.1 | Planned; 18 symptoms |
| [delivery/readiness-dashboard.md](delivery/readiness-dashboard.md) | 0.2 | Single status summary |
| [delivery/documentation-review.md](delivery/documentation-review.md) | 0.3 | Three review passes; 69 findings |
| [traceability/operation-coverage-matrix.md](traceability/operation-coverage-matrix.md) | 0.2 | Partial — derived, Planned |
| [reference/vocabulary.md](reference/vocabulary.md) | 0.1 | Controlled — single source for enumerated values |

## Former identifiers

Revision 0.1 used flat `DOC-XXX-001` filenames. Those identifiers survive in prose as stable citation keys — `DOC-ARCH-001 §6` still resolves — but the files now follow section paths. This table is the mapping.

| Identifier | Document |
|---|---|
| DOC-SRS-001 | [business/requirements.md](business/requirements.md) |
| DOC-ARCH-001 | [architecture/architecture-overview.md](architecture/architecture-overview.md) |
| DOC-ARCH-002 | [architecture/failure-modes.md](architecture/failure-modes.md) |
| DOC-ARCH-003 | [architecture/provider-adapters.md](architecture/provider-adapters.md) |
| DOC-DATA-001 | [data-and-contracts/data-model.md](data-and-contracts/data-model.md) |
| DOC-DATA-002 | [data-and-contracts/data-dictionary.md](data-and-contracts/data-dictionary.md) |
| DOC-DATA-003 | [data-and-contracts/migration-plan.md](data-and-contracts/migration-plan.md) |
| DOC-API-001 | [data-and-contracts/contract-conventions.md](data-and-contracts/contract-conventions.md) |
| DOC-API-002 | [data-and-contracts/api-catalog.md](data-and-contracts/api-catalog.md) |
| DOC-API-003 | [data-and-contracts/error-catalog.md](data-and-contracts/error-catalog.md) |
| DOC-OPS-001 | [operations/deployment-and-onboarding.md](operations/deployment-and-onboarding.md) |
| DOC-OPS-002 | [operations/provider-prerequisites.md](operations/provider-prerequisites.md) |
| DOC-OPS-003 | [operations/upgrade-and-rollback.md](operations/upgrade-and-rollback.md) |
| DOC-OPS-004 | [operations/backup-and-recovery.md](operations/backup-and-recovery.md) |
| DOC-OPS-005 | [operations/runbook.md](operations/runbook.md) |
| DOC-OPS-006 | [operations/storage-prerequisites.md](operations/storage-prerequisites.md) |
| DOC-UX-001 | [experience/dashboard-and-operations.md](experience/dashboard-and-operations.md) |
| DOC-UX-002 | [experience/accessibility.md](experience/accessibility.md) |
| DOC-UX-003 | [experience/wireframes-and-navigation.md](experience/wireframes-and-navigation.md) |
| DOC-UX-004 | [experience/visual-design.md](experience/visual-design.md) |
| DOC-ARCH-004 | [architecture/mcp-integration.md](architecture/mcp-integration.md) |
| DOC-TEST-001 | [testing/verification-plan.md](testing/verification-plan.md) |
| DOC-TEST-002 | [testing/traffic-harness.md](testing/traffic-harness.md) |
| DOC-OPS-007 | [operations/configuration-reference.md](operations/configuration-reference.md) |
| DOC-DEL-002 | [delivery/delivery-plan.md](delivery/delivery-plan.md) |
| DOC-DEL-003 | [delivery/ci-and-release.md](delivery/ci-and-release.md) |
| DOC-DEL-004 | [delivery/github-project.md](delivery/github-project.md) |
| DOC-UX-005 | [experience/screenshot-index.md](experience/screenshot-index.md) |
| DOC-SEC-001 | [security/threat-model.md](security/threat-model.md) |
| VOCABULARY | [reference/vocabulary.md](reference/vocabulary.md) |
| ADR-REGISTER | [architecture/decisions.md](architecture/decisions.md) |
| DECISION-LOG | [project/decisions-and-open-questions.md](project/decisions-and-open-questions.md) |

Identifiers are not reused and not retired. New documents are cited by path.

## Gates

No decision is open. The contract can be built against without waiting on anyone.

Three things are nonetheless unresolved, and none of them is a decision:

- **No code exists.** M0's exit criteria — a skeleton, `docker compose up`, `/capabilities` and preflight answering — are unmet.
- **No test exists.** Every acceptance criterion cites a case, and every case is `Planned`.
- **DEC-008's premise is unconfirmed.** If the adopter's clusters are not federated, that decision has no practical effect. `rabbitmq-plugins list | grep -E 'federation|shovel'` settles it in one command.

## Change discipline

Cite the owning document; never restate an enumerated value, a decision, or a metric definition in a second place. A second copy drifts, and the copy that drifts is always the one someone reads.

Record supersession explicitly — an identifier is never reused and never deleted. When a requirement, decision or table changes, update the owner and then reconcile every consumer that cites it. The 09 November 2026 pass added `revision` and `sample_rate` columns, split `payload_ref`, corrected a grant and registered a new enum; each change touched between three and eight documents.

Keep these categories separate and never let one imply another: **requirement**, **technical proposal**, **designed verification**, **executed evidence**, **sign-off**. At this baseline only the first three exist.
