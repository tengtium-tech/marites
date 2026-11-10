# Documentation standard

Version 0.1 | 07 November 2026 | Status: adopted

How this documentation set is organised and maintained. It is a working rule, not a manifesto: everything below exists because its absence caused a specific problem.

## Structure

Sections are folders. Each has a `README.md` that lists what exists and what is missing. Reading order is expressed in the [root index](../README.md) **and in the folder name**: a two-digit prefix that matches that order. The prefix is a navigation aid for a reader arriving from a file tree — it is not a version, not an identifier, and no `document_id` changes when a section is renumbered. The earlier convention carried no prefixes; it kept paths stable but left the order invisible outside the index, and the tree is the first thing a reader sees.

The fourteen sections, what each owns and what is present versus planned are defined once, in the [documentation structure](documentation-structure.md). This standard does not repeat them.

**A folder is not a specification.** A section index that lists two documents and four gaps is more honest than an empty folder implying coverage.

## Index-only sections

`platform-foundation` holds an index and no other document, because its content is owned by documents in other sections and a second copy would drift. The index states what is covered, by which document, and what is unwritten. A separate document is created only when its content outgrows its owner — which is what happened to `security` and `testing` on 09 November 2026, when a threat model and a verification plan respectively were written. No section holds a blank template.

## Naming

Descriptive kebab-case filenames: `contract-conventions.md`, not `DOC-API-001`. A filename should tell you what is inside without opening it.

Revision 0.1 used flat `DOC-XXX-001` names. Those identifiers survive as **citation keys in prose** — `DOC-ARCH-001 §6` still resolves — because rewriting several hundred inline references would risk introducing errors for no reader benefit. The [root index](../README.md) carries the mapping. New documents are cited by path.

## Headers

Two forms, by document type.

**Section indexes and the root index** open with a single line:

```
Version 0.2 | 07 November 2026 | Status: current
```

**Specifications** carry YAML front matter, so the version, date and status are machine-readable rather than only readable:

```
---
document_id: DOC-API-001
title: Contract Conventions
version: 0.1
date: 2026-11-07
status: Draft — binding on the contract
---
```

Status is always one of:

| Status | Meaning |
|---|---|
| Draft | Written, not reviewed |
| Adopted | Reviewed and in force |
| Controlled | Single source for the values it holds; changes require amendment first |
| Superseded | Retained for history; not authority |

## Readiness vocabulary

Used in dashboards, indexes and reviews. Do not invent other words.

| Word | Meaning |
|---|---|
| Not started | No substantive document exists |
| Drafted | Written to the intended scope and awaiting review |
| Partial | Written, with named gaps that change behaviour |
| Reviewed | A named review found no open finding at the stated scope |
| Proposed | Added by a specification or review and not yet accepted |
| Accepted | Chosen and recorded as a decision |
| Deferred | Deliberately postponed |
| Planned | Verification specified and not executed |
| Executed | Run, with linked evidence of the result |
| Unverified | Stated without evidence |

At this baseline nothing is Executed.

## Identifier register

Identifiers are stable. Do not renumber, reuse or delete one; supersede it and name the replacement. A prefix that is used but not registered here is a defect.

| Prefix | Form | Meaning | Defined in |
|---|---|---|---|
| `DEC` | DEC-nnn | Scope or policy question and its answer | [Decision register](decisions-and-open-questions.md) |
| `ADR` | ADR-nnn | Architecture decision | [ADR register](../05-architecture/decisions.md) |
| `FR` | FR-nnn | Functional requirement | [Requirements](../02-business/requirements.md) |
| `NFR` | NFR-nnn | Non-functional requirement | [Requirements](../02-business/requirements.md) |
| `C` | C-nnn | Constraint the design cannot cross | [Requirements](../02-business/requirements.md) §5 |
| `ASM` | ASM-nnn | Assumption | [Assumptions and risks](assumptions-and-risks.md) |
| `RSK` | RSK-nnn | Project risk | [Assumptions and risks](assumptions-and-risks.md) |
| `AUD` | AUD-nnn | Documentation review finding | [Documentation review](../12-delivery/documentation-review.md) |
| `TC` | TC-nnn | Verification case | [Verification plan](../10-testing/verification-plan.md) |
| `THR` | THR-nnn | Security threat | [Threat model](../09-security/threat-model.md) |
| `OP` | OP-AAA-nnn | API operation (AAA is three letters: CAP, BRK, QUE, MSG, MET, OPR, AUD) | [API catalog](../07-data-and-contracts/api-catalog.md) |
| `ERR` | ERR-AREA-nnn | Error code (AREA is one of CMN, AUTH, CAP, BRK, QUE, MSG, OPR, MET — `AUTH` is four letters, so the area is not a fixed width) | [Error catalog](../07-data-and-contracts/error-catalog.md) |
| `DOC` | DOC-XXX-nnn | Citation key from revision 0.1 | [Root index](../README.md) |
| `M` | Mn | Milestone, M0 to M7 | [Deployment and onboarding](../11-operations/deployment-and-onboarding.md) §6 |

`AUD-nnn` (a review finding) and the `AUD` area code inside `OP-AUD-001` (the audit-log operation) are different forms and do not collide. Capability policy names such as `CanReplay` are names, not numbered identifiers, and are owned by [VOCABULARY §2](../14-reference/vocabulary.md).

## Citation

- Cite the **owning** document. If two documents describe the same rule, one is wrong.
- Cite a **section number** where the reference is narrow: `DOC-API-001 §4`.
- Never restate an enumerated value, a decision or a metric definition. Link it. The second copy drifts, and the copy that drifts is always the one someone reads.

## Supersession

An identifier is never reused and never deleted. A withdrawn item is marked `Withdrawn` with its reason; a replaced one is marked `Superseded by <id>`. Both remain readable.

The live example in this repository is ADR-004, marked **Superseded by ADR-022** in the [decision register](../05-architecture/decisions.md): the reasoning is still readable, and no one can mistake it for the current answer.

## Evidence discipline

Five categories are never allowed to imply one another:

| Category | Present in this repository |
|---|---|
| Requirement | Yes |
| Technical proposal | Yes |
| Designed verification | Partly — traceability rows say "not yet designed" |
| Executed evidence | **No** |
| Sign-off | **No** |

A document is not evidence of implementation. No code exists, no test has run, and no behaviour has been observed against a broker. Where a document states what the system *will* do, it describes intent.

## Review

There is no independent review, because there is no second reviewer. Two passes exist: the 07 November 2026 [documentation review](../12-delivery/documentation-review.md) by the project author, and a 09 November 2026 completion pass that read the same documents and logged AUD-030 to AUD-044. Neither is independent of the documents it checks, and the second is not independent of the first's conclusions.

The substitute is mechanical: the contract is parsed and its references resolved on every change, every relative link is checked, every cited identifier is resolved against the document that defines it, and every claim of the form "N operations" or "N decisions" is checkable against the file that owns it. Numbers in prose that cannot be verified should not be written.
