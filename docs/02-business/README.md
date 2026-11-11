# Business requirements

Version 0.2 | November 2026 | Status: Approved — Product owner, 2026-11-11

What Marites must do, for whom, and what it will not do, independent of runtime, database, deployment or transport details.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Requirements (DOC-SRS-001)](requirements.md) | 29 functional and 8 non-functional requirements, each with one testable acceptance criterion and a milestone; actors, the four-step recovery loop, constraints C-001 to C-003, out-of-scope list and requirement traceability |
| [Verification plan](../10-testing/verification-plan.md) | The 29 cases the acceptance criteria cite, and the milestone gates they belong to |
| [Terms](../14-reference/vocabulary.md) | The glossary lives in VOCABULARY §11 (shadow queue, tap, relay, backfill, adopter, dead letter) |
| [Decision register](../01-project/decisions-and-open-questions.md) | The eight scope and policy questions that shaped the requirements |
| [Domain catalog](../04-domains/domain-catalog.md) | Five documentation areas and the requirements each serves |

## The product in one paragraph

Support engineers fix stuck message queues. Today that means SSH, the broker's own management UI, or a DBA. Marites gives them one screen: see what is failing and why, then retry, discard or publish without leaving the tool. A second audience, executives, needs volume and failure rates over months, and gets a separate reporting surface rather than a restricted version of the operational one.

## Constraints that shape everything

- **AMQP has no peek** (C-001). Observation must be non-destructive, which forces the shadow-binding design.
- **Capture is forward-only** (C-002). A binding sees messages published after it exists, so history needs a separate backfill.
- **The adopter supplies a configured broker** (C-003). A misconfigured broker produces an application that looks broken.

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
