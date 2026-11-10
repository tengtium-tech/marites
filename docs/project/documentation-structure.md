# Documentation structure

Version 0.1 | 07 November 2026 | Status: adopted navigation structure; content completeness is tracked separately in the [readiness dashboard](../delivery/readiness-dashboard.md).

## Purpose

Establish one consistent layout for project, business, workflow, domain, architecture, foundation, data/contract, experience, security, verification, operations, delivery and traceability material before implementation begins. Marites follows the same fourteen-section layout as the other projects in this portfolio, so a reader who knows one can navigate any of them.

A folder or index is not a completed specification. Each document lives in exactly one section, and each section index lists the records that exist and the work that is missing. Folder names carry no numeric prefix; reading order is expressed here and in the [root index](../README.md).

## Sections and reading order

| Section | Purpose | Present versus planned |
| --- | --- | --- |
| [project](README.md) | Decisions, assumptions, risks, the documentation standard and the changelog | Complete first draft, including the portfolio overview |
| [business](../business/README.md) | What Marites must do, for whom and what it will not do, independent of runtime or storage | Requirements with acceptance criteria drafted; no user research |
| [workflows](../workflows/README.md) | A message's path, an operator's path and an adopter's path | Five records: three lifecycles, failure paths, retention and compaction |
| [domains](../domains/README.md) | Five documentation areas with responsibilities, records and handoffs | Catalog drafted; per-area specifications live in the owning documents |
| [architecture](../architecture/README.md) | Boundaries, the one-hop tap, module map, capability model and every accepted decision | Drafted, including per-component failure modes and per-adapter design |
| [platform-foundation](../platform-foundation/README.md) | Reliable capture, identity, audit, configuration and process topology | Index only |
| [data-and-contracts](../data-and-contracts/README.md) | Tables, counting semantics, the HTTP contract, error codes | Drafted, including a column-level data dictionary and the first migration plan |
| [experience](../experience/README.md) | What the numbers mean on screen and who may act on them | Rules, wireframes, navigation, accessibility and visual design documented; no independent design review |
| [security](../security/README.md) | Credentials, payload responsibility, roles, confirmations and trust boundaries | Threat model drafted; no key-rotation procedure or TLS parameter set |
| [testing](../testing/README.md) | Verification the specification requires, and the gates added by review | Verification plan with 28 planned cases; nothing executed |
| [operations](../operations/README.md) | Adopter prerequisites, deployment, first run and milestones | All three providers' prerequisites drafted; runbook, upgrade and backup written |
| [delivery](../delivery/README.md) | Readiness summary, review records and milestone ownership | Dashboard and one review written |
| [traceability](../traceability/README.md) | Requirement, operation, milestone and verification crosswalks | Operation matrix carries levels and cases; requirement crosswalk in requirements §7 |
| [reference](../reference/README.md) | The controlled vocabulary: every enumerated value, defined once | Controlled |

## Index-only sections

`platform-foundation` contains an index and no further document. That is deliberate and is not a gap in the folder count: its content exists, but it is owned by documents in other sections, and a second copy would drift. The index lists the owning records and states what is still unwritten. A separate document is created only when its content outgrows the owner — which is what happened to `security` and `testing` on 09 November 2026, when the threat model and the verification plan were written. The same holds for `domains`: Marites has no commerce domain, so its catalog maps five documentation areas to the documents that specify them.

## Where Marites differs from a commerce product

| Concern | Placement |
| --- | --- |
| The controlled vocabulary (enumerated values, roles, capabilities, provider kinds) | `reference/vocabulary.md`, cited as VOCABULARY §n |
| Requirements | `business/requirements.md`, cited as DOC-SRS-001 |
| Role and capability matrix | `experience/dashboard-and-operations.md` §2, cited by `security` |
| Milestones | `operations/deployment-and-onboarding.md` §6; `delivery` links, never restates |
| Superseded material | `project/plan-v0.1-superseded.md` |

The former identifiers `DOC-*` survive as citation keys; the root index carries the mapping.

## Whole-system coverage standard

Call the documentation complete for the planned system only after:

- Every requirement is owned by a document, and every served operation by the contract.
- Each documentation area defines responsibilities, exclusions, records, rules and verification outcomes.
- Cross-area behaviour names who coordinates normal, failed, uncertain and compensating outcomes.
- Identity, audit, reliable capture, security, operations and delivery requirements have explicit owners and verification.
- Review status, implementation evidence, executed tests and deployment results are distinguishable. Neither a folder count nor a rendered diagram proves any of them.

At this baseline the specification is a drafted first pass. No code, test or deployment exists.

## Maintenance

Create substantive documents when their content exists; do not populate the structure with blank templates. Preserve identifiers and links. When a document is added or moved, update the affected section indexes, the root register and the traceability crosswalk in the same change. Distinguish planned work, drafted documentation, implemented behaviour and executed evidence.
