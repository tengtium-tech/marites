---
document_id: DOC-DEL-004
title: GitHub Project
product: Marites
---

# GitHub project

How this document set becomes a repository: the layout, the labels, the issue templates, the board, and the conventions that keep the two in step. It is a setup guide — read once, edited rarely.

## 1. Repository layout

```
marites/
├── docs/                 the specification set (this tree)
├── src/                  the service — Marites.Api, .Worker, .Contracts, .Core
├── web/                  the React client, generated from the contract
├── contract-tests/       the black-box conformance suite
├── testing/
│   ├── compose.yaml      broker stacks, one service per provider
│   ├── scenarios/        traffic-harness scenario files
│   └── fixtures/         the harness itself
├── migrations/           one folder per store
├── deploy/               compose, Helm chart, manifests
├── .github/              workflows, issue and PR templates
└── README.md
```

**`docs/` stays at the root and stays authoritative.** The specification is not wiki material that drifts from the code; it is reviewed, versioned and gated by CI like the code is.

## 2. Labels

Three axes, and nothing else. A label that is not one of these is a tag someone will stop using.

| Axis | Labels |
|---|---|
| **Type** | `type: feature` · `type: defect` · `type: decision` · `type: doc` · `type: chore` |
| **Area** | `area: capture` · `area: operations` · `area: audit` · `area: storage` · `area: ui` · `area: agent` · `area: providers` · `area: docs` |
| **State** | `needs-decision` · `blocked` · `quarantined` |

**Milestones carry the sequence, not labels.** `M0` … `M7` are GitHub milestones, so the board can group by them without a second taxonomy. `M7` exists because [the delivery plan](delivery-plan.md) added it.

## 3. Issue templates

Four templates, one per kind of work. Each exists because a category of issue was being written badly without one.

**Work package** — the default. Title: `WP-M1-3 Hold and promote`.

```markdown
## What this finishes
<one paragraph, from the delivery plan>

## Definition of done
- [ ] Behaviour specified (FR / ADR) and documents agree
- [ ] Cases executed: TC-…
- [ ] Failure path and error code
- [ ] No other document describes the old behaviour

## References
Requirement: FR-0xx · Decision: ADR-0xx · Package: WP-Mx-y
```

**Defect** — reproduction first, opinion second.

```markdown
## What happens / what should happen
## Reproduction
1. …
## Evidence
- Error code: ERR-…
- Audit row: …
- Version: …
```

**Decision needed** — the issue that closes a gap in the documents.

```markdown
## The question, in plain terms
## Options, with the cost of each
## What it blocks
Owner: client | technical
```

**Doc change** — because a documentation change has a propagation rule the other templates do not carry.

```markdown
## What is wrong, and where
## Which documents must move with it
- [ ] …
```

## 4. Project fields and views

| Field | Type | Why |
|---|---|---|
| `Status` | Backlog · Ready · In progress · Review · Done | The only field that needs maintaining by hand |
| `Milestone` | native | Grouping, and the release train |
| `Area` | single select, mirrors the label | Filtering without label soup |
| `Package` | text (`WP-Mx-y`) | Ties the issue to the delivery plan without a lookup |
| `Size` | S · M · L | Deliberately not hours. A size that means hours becomes a promise |

| View | Shows |
|---|---|
| **Board** | Grouped by `Status`, filtered to the current milestone — the one people work from |
| **Roadmap** | Grouped by milestone, no dates — the order, not a schedule |
| **Needs decision** | `needs-decision` open issues; the queue that blocks everything downstream |
| **Quarantine** | `quarantined` — flaky tests and known-broken gates, so they are not forgotten |

## 5. Automation

Deliberately small. Every automation is one that cannot be wrong in a way nobody notices.

| When | Then |
|---|---|
| An issue is opened | Added to the project, `Status = Backlog` |
| A pull request references an issue (`Closes #12`) | The issue moves to `In progress` |
| The pull request merges | The issue closes and moves to `Done` |
| A `needs-decision` issue closes | Nothing automatic — the decision register is updated by hand, in the same commit that records the answer |

**The last row is the important one.** Closing a decision issue does not update `docs/project/decisions-and-open-questions.md`, and pretending it could is how a register and a board drift apart. The document is the record; the issue is the conversation.

## 6. Branch and pull request conventions

| Branch | From | Named |
|---|---|---|
| `wp/<package-id>` | `main` | `wp/m1-3-hold-and-promote` |
| `fix/<short>` | `main` | `fix/peek-requeue-loop` |
| `docs/<short>` | `main` | `docs/two-stores` |

**Squash merge, one package per pull request.** A pull request that closes two packages cannot be reviewed against either definition of done.

The PR template asks for four things and refuses to be longer:

```markdown
## What changed, and why
## Which specification it implements
FR-… / ADR-… / DEC-…
## Which cases were executed
TC-… (or: none — and why that is acceptable)
## Anything a reader should doubt
```

**That last heading is the one that earns its place.** It is where an author writes *"the concurrency path is untested"* or *"this works for RabbitMQ only"* — the sentence that would otherwise be discovered three months later in an incident.

## 7. What is not automated

- **No issue is opened by a document change.** The documents are reviewed in pull requests like code; a decision that creates work opens its own package.
- **No status is derived from the dashboard.** The dashboard is written by hand from the gates, and a board that computed it would be a second source of truth.
- **No coverage badge.** Coverage of a changed file is a gate; a percentage on the README is a number nobody acts on.

## 8. Document control

Revision 0.1, 10 November 2026. No repository exists yet: this is the setup the first commit follows, and the templates are written to be pasted into `.github/`. The label and field lists are intentionally short — the failure mode of a project board is taxonomy, not scarcity.
