# Traceability and review

Version 0.2 | November 2026 | Status: Partial; two derived crosswalks, both Planned.

Connect requirements, decisions, operations, milestones and verification using stable identifiers.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Requirement traceability](../02-business/requirements.md) (§7) | Each requirement group tied to its owning specification and to the cases that would prove it |
| [Operation coverage matrix](operation-coverage-matrix.md) | Every operation tied to its requirement, role, milestone, verification level and cases |
| [Verification plan](../10-testing/verification-plan.md) | The 28 cases and eight milestone gates the crosswalks cite |
| [Decision register](../01-project/decisions-and-open-questions.md) | DEC-001 to DEC-019 and what each constrains |
| [Architecture decision register](../05-architecture/decisions.md) | ADR-001 to ADR-023 (22 accepted, 1 superseded) |
| [Identifier register](../01-project/documentation-standard.md) | Every identifier prefix and its canonical document |

## The three crosswalks, and which one is authoritative

| Crosswalk | Lives in | Answers |
|---|---|---|
| Requirement → criterion → case | [Requirements §3.1, §4.1](../02-business/requirements.md) | What must be true, and which case proves it |
| Requirement group → owning specification | [Requirements §7](../02-business/requirements.md) | Which document owns the requirement |
| Operation → requirement → role → milestone → case | [Operation matrix](operation-coverage-matrix.md) | Which operation serves which requirement, and who may call it |

Three tables rather than one, because they answer different questions and are edited for different reasons. The rule that keeps them from drifting: **a case is defined once, in the verification plan, and cited everywhere else.**

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
