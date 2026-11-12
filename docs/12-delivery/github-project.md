---
document_id: DOC-DEL-004
title: GitHub Project and branch workflow
product: Marites
---

# GitHub Project and branch workflow

This guide explains how the shared specification, implementation branches, GitHub Projects, milestones, and work issues fit together.

## 1. Repository and branch model

The repository has one shared specification baseline and separate implementation branches:

| Branch | Purpose | Work tracking |
|---|---|---|
| `main` | Authoritative shared requirements, workflows, domain rules, API contract, acceptance criteria, and technology-neutral decisions | Shared issues where needed |
| `react` | React/TypeScript frontend implementation | [Marites — React Frontend Project](https://github.com/users/tengtium-tech/projects/6) |
| `csharp` | C#/.NET backend implementation of the shared API contract | [Marites — C# Backend Project](https://github.com/users/tengtium-tech/projects/7) |
| `react-csharp` | Pairing guide, links, and instructions for running the React and C# branches together | Links to both Projects |

Git branches are full repository snapshots, not overlays. The implementation branches therefore inherit shared files from `main`; those are not separately authored copies. `main` remains the source of truth for shared specifications. After shared documentation changes are merged to `main`, merge `main` into the implementation branches so they receive the update without creating a competing version.

Keep stack-specific implementation code and guidance on its stack branch. The current documentation baseline still includes some first-profile React and C# architecture guidance on `main`; during discovery, classify those documents and move or adapt profile-only guidance to the matching stack branch while retaining shared behavior and contract rules on `main`. Do not maintain two competing authoritative copies. Keep pairing instructions on `react-csharp`; that branch does not own frontend or backend application code. Other frontend and backend profiles can follow the same model when they are created.

**The pairing branch also owns the deployment composition.** A compose file describing both stacks cannot live on either one: `csharp` has no web app and `react` has no API, so the file would dangle from whichever branch held it. `react-csharp` is the one branch where running the two together is the subject, so `deploy/` lives there, with build contexts pointing at the two stack worktrees. This is deployment configuration, not application source — the rule above still holds: the pairing branch contains no frontend or backend code of its own.

## 2. GitHub Projects and work items

Use one Project for the reusable React frontend and one Project for each backend implementation. The current Projects are:

- [Marites — React Frontend](https://github.com/users/tengtium-tech/projects/6)
- [Marites — C# Backend](https://github.com/users/tengtium-tech/projects/7)

Each Project uses Kanban statuses **Backlog**, **Ready**, **In Progress**, **In Review**, and **Done**. The Kanban view uses milestones M0–M7 to group delivery scope; the Sprint Backlog view groups work by the two-week Monday-start iteration. Sprint 1 begins November 16, 2026.

A repository issue is the work item. Projects display and organize those issues; they are not separate copies of a task list. A cross-stack issue may be added to both Projects so each workstream can see the same source issue. Milestones represent delivery outcomes; sprints represent the timebox for work the team has committed to.

The [delivery plan](delivery-plan.md) defines work packages and their outcomes. The Projects track their discovery, priority, status, milestone, and sprint assignment.

## 3. Issue templates and discovery

Use a work-package issue for a scoped outcome, a decision issue for an unresolved question, a defect issue for reproducible incorrect behavior, and a documentation issue for a specification change.

Before moving a work package from **Backlog** to **Ready**, discovery should make these points clear:

- Outcome, in-scope behavior, and exclusions.
- Owning workstream and target branch: React/`react`, C#/.NET/`csharp`, or shared/`main`.
- Dependencies and the order in which they must be satisfied.
- Acceptance criteria, applicable requirements and decisions, and how the result will be verified.
- Known risks and unresolved questions. Record an unresolved question as a decision issue and link it to the work package.

A useful work-package description follows this shape:

```markdown
## What this finishes
<The observable outcome and scope.>

## Discovery and dependencies
<Owner, target branch, prerequisites, and decisions still needed.>

## Acceptance criteria
- [ ] <Observable result>
- [ ] <Failure behavior and error handling>
- [ ] <Verification case or review evidence>

## References
Requirement: FR-… · Decision: ADR/DEC-… · Package: WP-Mx-y
```

Do not assign work to a sprint until it is ready and the team has chosen to commit to it. Discovery may split a large work package into linked sub-issues when the pieces can be reviewed and delivered independently; keep the parent issue as the outcome and link each child task to it.

The working cycle is **Discover → Ready → Sprint commitment → Implement → Review and verify → Merge → Sync**. Keep work in Backlog while scope, dependencies, acceptance, or decisions remain unclear. Move it to Ready only when those points are understood; move Ready work into a sprint only when the team commits to it. This keeps discovery separate from delivery promises.

For a decision issue, state the question plainly, list viable options and their consequences, explain what work it blocks, and name who will resolve it. When the decision affects shared product behavior or the API contract, record the accepted result on `main` and link the issue from both Projects as appropriate.

## 4. Milestones and completion

Milestones M0–M7 carry delivery order. The [delivery plan](delivery-plan.md) contains each package, dependencies, and definition of done; the milestone exit gates remain in [deployment and onboarding §6](../11-operations/deployment-and-onboarding.md).

A work package is complete only when its specified behavior is implemented, applicable verification has been run, failure paths and error codes are covered, related documents agree, and no unrun evidence is claimed. Closing an issue alone does not satisfy a milestone gate.

## 5. Branch and pull-request conventions

The four branches above are the long-lived shared and integration branches. Create short-lived task branches from the implementation branch that owns the work, then open a pull request back to that same branch.

| Work | Start from | Example task branch | Pull request target |
|---|---|---|---|
| React task | `react` | `wp/m0-7-setup-wizard` | `react` |
| C# foundation or feature task | `csharp` | `wp/m0-2-dotnet-skeleton` | `csharp` |
| Shared specification change | `main` | `docs/contract-error-shape` | `main` |
| Pairing-guide change | `react-csharp` | `docs/react-csharp-local-run` | `react-csharp` |

Use the work-package id in implementation branch names where a package exists. A C# foundation task is still a task branch from `csharp`; a feature is another short-lived task branch from `csharp`. Do not create permanent `foundation` or `features` branches. Merge shared `main` changes into `react` and `csharp` after they land on `main` so each implementation stays aligned with the authoritative contract and requirements.

Do not commit task implementation directly to `react` or `csharp`; use a task branch and review it through a pull request. Link the pull request to its issue, keep it focused on one package, and include the acceptance evidence and verification performed. Stack-specific formatting, build, and verification commands belong in that stack branch's README so contributors can follow the same practice.

Keep pull requests focused on one work package or coherent documentation change. Link the issue, state which requirements or decisions it implements, describe the verification performed (or why none is appropriate), and disclose known gaps. Cross-stack issues may need separate implementation pull requests into `react` and `csharp`, both linked to the same issue.

## 6. Labels

Use labels for work type and area; use milestones for delivery sequence and the Sprint field for iteration.

| Axis | Examples |
|---|---|
| Type | `type: feature`, `type: defect`, `type: decision`, `type: doc`, `type: chore` |
| Area | `area: capture`, `area: operations`, `area: audit`, `area: storage`, `area: ui`, `area: agent`, `area: providers`, `area: docs` |
| State | `needs-decision`, `blocked`, `quarantined` |

Keep labels concise. Do not make a label for a milestone, sprint, branch, or status that already has a Project field.

## 7. What stays manual

Issues and documents are reviewed by people. A closed decision issue does not update the decision register automatically; update the relevant decision record and shared specification in the same change that records the accepted result. Project status, sprint assignment, and milestone readiness must reflect actual work and evidence rather than assumptions.

## 8. Keeping this guide current

Update this guide when the branch model, Project setup, or contribution workflow changes. Keep shared behavior and contract decisions authoritative on `main`. Synchronising them into the implementation branches is not a convention to remember: `.github/workflows/docs-sync.yml` merges `main` into `csharp`, `react` and `react-csharp` on every push that touches `docs/`, and pushes only when the branch is behind. A conflict fails that job and names the branch, because a conflict means a shared document was edited on a stack branch — a classification decision for a person, not something a merge should guess at. If a stack branch is protected, the push is rejected and the job fails; fix the permission rather than disabling the job, since a silent skip is indistinguishable from no sync at all.
