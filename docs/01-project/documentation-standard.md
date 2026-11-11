# Documentation standard

How this documentation set is organised and maintained. It is a working rule, not a manifesto: everything below exists because its absence caused a specific problem.

## Structure

Sections are folders. Each has a `README.md` that lists what exists and what is missing. Reading order is expressed in the [root index](../README.md) **and in the folder name**: a two-digit prefix that matches that order. The prefix is a navigation aid for a reader arriving from a file tree — it is not a version, not an identifier, and no `document_id` changes when a section is renumbered. The earlier convention carried no prefixes; it kept paths stable but left the order invisible outside the index, and the tree is the first thing a reader sees.

The fourteen sections, what each owns and what is present versus planned are defined once, in the [documentation structure](documentation-structure.md). This standard does not repeat them.

**A folder is not a specification.** A section index that lists two documents and four gaps is more honest than an empty folder implying coverage.

## Index-only sections

`platform-foundation` holds an index and no other document, because its content is owned by documents in other sections and a second copy would drift. The index states what is covered, by which document, and what is unwritten. A separate document is created only when its content outgrows its owner — which is what happened to `security` and `testing` in November 2026, when a threat model and a verification plan respectively were written. No section holds a blank template.

## Naming

Descriptive kebab-case filenames: `contract-conventions.md`, not `DOC-API-001`. A filename should tell you what is inside without opening it.

Revision 0.1 used flat `DOC-XXX-001` names. Those identifiers survive as **citation keys in prose** — `DOC-ARCH-001 §6` still resolves — because rewriting several hundred inline references would risk introducing errors for no reader benefit. The [root index](../README.md) carries the mapping. New documents are cited by path.

## Headers

Two forms, by document type.

**Section indexes and the root index** open with a single line:

```
Version 0.2 | November 2026 | Status: current
```

**Specifications** carry YAML front matter, so the version, date and status are machine-readable rather than only readable:

```
---
document_id: DOC-API-001
title: Contract Conventions
version: 0.1
date: 2026-11
status: Draft — binding on the contract
---
```

Status is always one of:

| Status | Meaning |
|---|---|
| Draft | Written, not reviewed |
| Reviewed | A named documentation review found no open finding at the stated scope |
| Approved | The document owner accepts a Reviewed baseline; record the approver and date in document control. This does not imply implementation or verification has run |
| Adopted | Reviewed and in force as a project standard |
| Controlled | Single source for the values it holds; changes require amendment first |
| Superseded | Retained for history; not authority |

**Approved is the highest status this set uses, and it is not a claim that anything works.** It records that the owner accepted a Reviewed baseline; no application code exists, no case has been executed, and no deployment has been performed.

**Baseline and revision are different numbers.** Each document carries its own `version`, which records how often that document changed. The set carries one **baseline**, stated in the [documentation index](../README.md) and the [root readme](../../README.md) — 1.0 at this freeze. A document at revision 0.2 inside baseline 1.0 is not a contradiction: the first says how many times the file changed, the second says which snapshot the set is at. Only the baseline moves to 1.0; flattening every document to the same number would erase the revision history each document-control section records.

## Readiness vocabulary

Used in dashboards, indexes and reviews. Do not invent other words.

| Word | Meaning |
|---|---|
| Not started | No substantive document exists |
| Drafted | Written to the intended scope and awaiting review |
| Partial | Written, with named gaps that change behaviour |
| Reviewed | A named review found no open finding at the stated scope |
| Approved | The document owner accepts a Reviewed baseline; record the approver and date in document control. This does not imply implementation or verification has run |
| Proposed | Added by a specification or review and not yet accepted |
| Accepted | Chosen and recorded as a decision |
| Deferred | Deliberately postponed |
| Planned | Verification specified and not executed |
| Executed | Run, with linked evidence of the result |
| Unverified | Stated without evidence |

At this baseline no application verification case is `Executed`. Static documentation and contract checks are recorded separately and do not count as application evidence.

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
| `TC` | TC-nnn | Verification case | [Verification plan](../10-testing/verification-plan.md) |
| `THR` | THR-nnn | Security threat | [Threat model](../09-security/threat-model.md) |
| `OP` | OP-AAA-nnn | API operation (AAA is three letters: CAP, BRK, QUE, MSG, MET, OPR, AUD) | [API catalog](../07-data-and-contracts/api-catalog.md) |
| `ERR` | ERR-AREA-nnn | Error code (AREA is one of CMN, AUTH, CAP, BRK, QUE, MSG, OPR, MET, AUD, MIG — `AUTH` is four letters, so the area is not a fixed width) | [Error catalog](../07-data-and-contracts/error-catalog.md) |
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
| Designed verification | Yes — requirements cite named planned cases; none has been executed |
| Executed evidence | **No** |
| Sign-off | Documentation baseline approved by the project owner; no implementation or release sign-off |

A document is not evidence of implementation. No test has run, and no behaviour has been observed against a broker. The recorded sign-off accepts this documentation baseline only. Where a document states what the system *will* do, it describes intent.

## Review

The set is reviewed mechanically, because a solo project has no second reviewer. `tools/docs_check.py` is that review: it resolves every relative link and anchor, resolves every cited identifier against the document that defines it, checks every stated total against the file that owns the number, and asserts that each requirement has exactly one acceptance row. It exits non-zero on a finding, so it can gate a merge; it is not yet wired to a pipeline, which is why it is run by hand and why defects survive between passes.

**It checks that the documents agree with themselves, and nothing more.** It cannot tell whether a statement is true of the system, because there is no system. Numbers in prose that no file owns cannot be checked by it and should not be written.
