# Marites documentation

Version 1.0 | November 2026 | Status: Approved — Product owner, 2026-11-11

Marites observes message traffic crossing an adopter-supplied messaging platform — RabbitMQ, Azure Service Bus or Google Pub/Sub — and lets authorised operators count it, inspect it, and recover from failures. It is a **client** of a broker the adopter already runs: it never provisions, hosts, upgrades or repairs one.

The approved scope is settled. All nineteen recorded decisions are answered, and no unresolved product decision blocks M0 planning. The M0 exit gate is unimplemented. Every requirement carries an acceptance criterion and cites a planned case — none has been executed.

## Reading order

The documentation uses numbered sections — `01-project` to `14-reference` — matching the reading order below. Each document has one authoritative location. The [documentation structure](01-project/documentation-structure.md) defines placement and maintenance; the [area catalog](04-domains/domain-catalog.md) maps responsibilities without selecting deployable boundaries. Each section index states what exists and what is still missing. A folder is not a completed specification.

| Section | Contents |
| --- | --- |
| [Project](01-project/README.md) | The [portfolio overview](01-project/portfolio-overview.md), shared specification and implementation-profile strategy, decisions, assumptions and risks. Read first |
| [Business](02-business/README.md) | Requirements with acceptance criteria, actors, constraints and scope |
| [Workflows](03-workflows/README.md) | The capture, recovery and onboarding lifecycles, failure paths, and the retention and compaction lifecycle |
| [Domains](04-domains/README.md) | Five documentation areas with responsibilities, records and handoffs |
| [Architecture](05-architecture/README.md) | The one-hop tap, module map, capability model, component failure modes, adapter design and every accepted decision |
| [Platform foundation](06-platform-foundation/README.md) | Reliable capture, identity, audit, configuration and process topology (index) |
| [Data and contracts](07-data-and-contracts/README.md) | Tables, the data dictionary, the migration plan, counting semantics, the HTTP contract and error codes |
| [Experience](08-experience/README.md) | Metric presentation, role capabilities, wireframes and navigation, accessibility |
| [Security](09-security/README.md) | The [threat model](09-security/threat-model.md), credentials, payload responsibility and operation safeguards |
| [Testing](10-testing/README.md) | The [verification plan](10-testing/verification-plan.md): levels, 30 cases, eight gates; nothing executed |
| [Operations](11-operations/README.md) | Adopter prerequisites for all three providers, deployment, upgrade, backup, runbook and milestones |
| [Delivery](12-delivery/README.md) | The [readiness dashboard](12-delivery/readiness-dashboard.md), the delivery plan, CI and the project board |
| [Traceability](13-traceability/README.md) | The operation-to-requirement matrix and the verification crosswalks |
| [Reference](14-reference/README.md) | The controlled vocabulary |

For a first read: [requirements](02-business/requirements.md) §2, then the [three lifecycles](03-workflows/README.md), then [architecture overview](05-architecture/architecture-overview.md) §2 and §3, then the [data model](07-data-and-contracts/data-model.md) §3, then the [served contract](07-data-and-contracts/openapi/openapi.yaml).

## Current status

| Artifact | Status |
| --- | --- |
| Documentation baseline | **Baseline 1.0**, approved by the project owner on 2026-11-11 across 48 substantive documents, 14 section indexes and two nested indexes — 65 Markdown files counting this page |
| Requirements and decisions | Approved. 29 FR, 8 NFR, each with one acceptance criterion; 19 DEC answered, 25 ADRs (23 accepted, ADR-004 and ADR-005 superseded) |
| Architecture and data model | Approved design. Twelve tables in two stores, four backend layers, four ports, nine components with failure modes |
| API contract | Approved: 10 served and 15 deferred OpenAPI 3.2.1 operations; both pass static lint, running-service and generated-client conformance remain Planned |
| Verification | Approved plan only: 30 cases, 8 gates, 0 executed — [verification plan](10-testing/verification-plan.md) |
| Traceability | Approved — all 29 FRs, 8 NFRs and 25 API operations link to owning specifications and planned cases |
| Security | Approved design. 26 threats; six planned cases cover ten, six stated residuals; no penetration test |
| Implementation status | Not recorded here. `main` holds the specification; each stack branch records what it has built in its own `STATUS.md` |

## Authority and interpretation

When two documents disagree, this table decides which one wins.

| Concept | Owner |
|---|---|
| Requirements, priorities, scope, acceptance criteria | [business/requirements.md](02-business/requirements.md) |
| Canonical enumerated values, roles, capabilities, metric names, identifiers | [reference/vocabulary.md](14-reference/vocabulary.md) |
| Settled architecture decisions | [architecture/decisions.md](05-architecture/decisions.md) |
| Open questions and their status | [project/decisions-and-open-questions.md](01-project/decisions-and-open-questions.md) |
| Broker prerequisites and adopter obligations | [operations/deployment-and-onboarding.md](11-operations/deployment-and-onboarding.md) §1; [provider-prerequisites.md](11-operations/provider-prerequisites.md) for Service Bus and Pub/Sub |
| Table structures and relationships | [data-and-contracts/data-model.md](07-data-and-contracts/data-model.md) |
| Column types, nullability, defaults and constraints | [data-and-contracts/data-dictionary.md](07-data-and-contracts/data-dictionary.md) |
| Counting semantics and metric definitions | [data-and-contracts/data-model.md](07-data-and-contracts/data-model.md) §3; [experience/dashboard-and-operations.md](08-experience/dashboard-and-operations.md) §1 |
| HTTP rules — outcomes, idempotency, paging, versioning | [data-and-contracts/contract-conventions.md](07-data-and-contracts/contract-conventions.md) |
| Error codes | [data-and-contracts/error-catalog.md](07-data-and-contracts/error-catalog.md) |
| The API surface itself | [data-and-contracts/openapi/openapi.yaml](07-data-and-contracts/openapi/openapi.yaml) |
| Repository organisation | [architecture/architecture-overview.md](05-architecture/architecture-overview.md) §6 |
| Component failure behaviour | [architecture/failure-modes.md](05-architecture/failure-modes.md) |
| Verification levels, cases and gates | [testing/verification-plan.md](10-testing/verification-plan.md) |
| How test traffic is produced and seeded | [testing/traffic-harness.md](10-testing/traffic-harness.md) |
| Every configuration variable, in one table | [operations/configuration-reference.md](11-operations/configuration-reference.md) |
| What the work is, and what blocks it | [delivery/delivery-plan.md](12-delivery/delivery-plan.md) |
| Every screen and dialog, as an image | [experience/screenshot-index.md](08-experience/screenshot-index.md) |
| Threats and their controls | [security/threat-model.md](09-security/threat-model.md) |
| Screen layout and navigation | [experience/wireframes-and-navigation.md](08-experience/wireframes-and-navigation.md) |
| Accessibility requirements | [experience/accessibility.md](08-experience/accessibility.md) |
| Identifiers and readiness vocabulary | [project/documentation-standard.md](01-project/documentation-standard.md) |
| Assumptions and risks | [project/assumptions-and-risks.md](01-project/assumptions-and-risks.md) |
| Overall readiness | [delivery/readiness-dashboard.md](12-delivery/readiness-dashboard.md) |
| Operation-to-requirement mapping | [traceability/operation-coverage-matrix.md](13-traceability/operation-coverage-matrix.md) |
| Verification | Planned; no test code or execution evidence exists. |

**A document is not evidence of implementation.** Static screenshots illustrate the proposed UI; no production service has been built, run, or verified against a broker. Statements about product behaviour describe intent.

## Shared specification and implementation profiles

The product contract is shared: requirements, user-visible workflows, domain behavior, the OpenAPI surface, security rules, and acceptance criteria apply to every stack. The first implementation profile is React/TypeScript with C#/.NET. Some current architecture, persistence, operations, and delivery documents contain choices for that profile; those choices do not bind later profiles. See the [portfolio overview](01-project/portfolio-overview.md) for the planned frontend/backend matrix and repository strategy.

**Paths in prose are relative to this `docs/` directory.** The contract lives inside the documentation because it is the product boundary, not a build artefact.

## Former identifiers

Revision 0.1 used flat `DOC-XXX-001` filenames. Those identifiers survive in prose as stable citation keys — `DOC-ARCH-001 §6` still resolves — but the files now follow section paths. This table is the mapping.

| Identifier | Document |
|---|---|
| DOC-SRS-001 | [business/requirements.md](02-business/requirements.md) |
| DOC-ARCH-001 | [architecture/architecture-overview.md](05-architecture/architecture-overview.md) |
| DOC-ARCH-002 | [architecture/failure-modes.md](05-architecture/failure-modes.md) |
| DOC-ARCH-003 | [architecture/provider-adapters.md](05-architecture/provider-adapters.md) |
| DOC-DATA-001 | [data-and-contracts/data-model.md](07-data-and-contracts/data-model.md) |
| DOC-DATA-002 | [data-and-contracts/data-dictionary.md](07-data-and-contracts/data-dictionary.md) |
| DOC-DATA-003 | [data-and-contracts/migration-plan.md](07-data-and-contracts/migration-plan.md) |
| DOC-API-001 | [data-and-contracts/contract-conventions.md](07-data-and-contracts/contract-conventions.md) |
| DOC-API-002 | [data-and-contracts/api-catalog.md](07-data-and-contracts/api-catalog.md) |
| DOC-API-003 | [data-and-contracts/error-catalog.md](07-data-and-contracts/error-catalog.md) |
| DOC-OPS-001 | [operations/deployment-and-onboarding.md](11-operations/deployment-and-onboarding.md) |
| DOC-OPS-002 | [operations/provider-prerequisites.md](11-operations/provider-prerequisites.md) |
| DOC-OPS-003 | [operations/upgrade-and-rollback.md](11-operations/upgrade-and-rollback.md) |
| DOC-OPS-004 | [operations/backup-and-recovery.md](11-operations/backup-and-recovery.md) |
| DOC-OPS-005 | [operations/runbook.md](11-operations/runbook.md) |
| DOC-OPS-006 | [operations/storage-prerequisites.md](11-operations/storage-prerequisites.md) |
| DOC-UX-001 | [experience/dashboard-and-operations.md](08-experience/dashboard-and-operations.md) |
| DOC-UX-002 | [experience/accessibility.md](08-experience/accessibility.md) |
| DOC-UX-003 | [experience/wireframes-and-navigation.md](08-experience/wireframes-and-navigation.md) |
| DOC-UX-004 | [experience/visual-design.md](08-experience/visual-design.md) |
| DOC-ARCH-004 | [architecture/mcp-integration.md](05-architecture/mcp-integration.md) |
| DOC-TEST-001 | [testing/verification-plan.md](10-testing/verification-plan.md) |
| DOC-TEST-002 | [testing/traffic-harness.md](10-testing/traffic-harness.md) |
| DOC-OPS-007 | [operations/configuration-reference.md](11-operations/configuration-reference.md) |
| DOC-DEL-002 | [delivery/delivery-plan.md](12-delivery/delivery-plan.md) |
| DOC-DEL-003 | [delivery/ci-and-release.md](12-delivery/ci-and-release.md) |
| DOC-DEL-004 | [delivery/github-project.md](12-delivery/github-project.md) |
| DOC-UX-005 | [experience/screenshot-index.md](08-experience/screenshot-index.md) |
| DOC-SEC-001 | [security/threat-model.md](09-security/threat-model.md) |
| VOCABULARY | [reference/vocabulary.md](14-reference/vocabulary.md) |
| ADR-REGISTER | [architecture/decisions.md](05-architecture/decisions.md) |
| DECISION-LOG | [project/decisions-and-open-questions.md](01-project/decisions-and-open-questions.md) |

Identifiers are not reused and not retired. New documents are cited by path.

## Gates

No decision is open. The contract can be built against without waiting on anyone.

Three things are nonetheless unresolved, and none of them is a decision:

- **No code exists.** M0's exit criteria — a skeleton, `docker compose up`, `/capabilities` and preflight answering — are unmet.
- **No test exists.** Every acceptance criterion cites a case, and every case is `Planned`.
- **DEC-008's premise is unconfirmed.** If the adopter's clusters are not federated, that decision has no practical effect. `rabbitmq-plugins list | grep -E 'federation|shovel'` settles it in one command.

## Change discipline

Cite the owning document; never restate an enumerated value, a decision, or a metric definition in a second place. A second copy drifts, and the copy that drifts is always the one someone reads.

Record supersession explicitly — an identifier is never reused and never deleted. When a requirement, decision or table changes, update the owner and then reconcile every consumer that cites it. The November 2026 pass added `revision` and `sample_rate` columns, split `payload_ref`, corrected a grant and registered a new enum; each change touched between three and eight documents.

Keep these categories separate and never let one imply another: **requirement**, **technical proposal**, **designed verification**, **executed evidence**, **sign-off**. At this baseline only the first three exist.
