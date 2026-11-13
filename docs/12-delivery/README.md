# Delivery management

Keep the documentation baseline, milestone ownership and review evidence traceable.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Readiness dashboard](readiness-dashboard.md) | Single summary of readiness by layer, the M0 exit gate, and the four items that would change the page most |
| [Verification plan](../10-testing/verification-plan.md) (§3) | The eight milestone gates that decide whether a milestone exits |
| [Delivery plan](delivery-plan.md) | Work packages `WP-M0-1` onward, the definition of done, what blocks what, and what is deliberately not scheduled. Milestone names stay owned by operations; M7 is added here because two documents already assumed it |
| [CI and release](ci-and-release.md) | What runs on a pull request versus a tag, the ten gates, versioning and migrations, artifacts, and the release checklist |
| [GitHub Project and branch workflow](github-project.md) | Shared documentation ownership, persistent stack branches, separate frontend/backend Projects, discovery, Kanban, milestones, sprints, and pull-request conventions |
| [Milestones M0 to M7](../11-operations/deployment-and-onboarding.md) (§6) | Owned by operations; this section links and never restates them |
| [Assumptions and risks](../01-project/assumptions-and-risks.md) | ASM and RSK records that gate the milestones |

## The distinction this section exists to keep

| Category | Present | Evidence |
| --- | --- | --- |
| Requirement | Yes | 29 FR, 8 NFR with acceptance criteria |
| Technical proposal | Yes | 25 ADRs (23 accepted, 2 superseded), 20 decisions, thirteen current tables, a contract |
| Designed verification | Yes | 30 cases, 8 gates, all `Planned` |
| Executed evidence | **No** | No test has run |
| Documentation approval | **Yes** | The project owner approved the documentation baseline on 2026-11-11 |
| Independent review / implementation sign-off | **No** | No second party has reviewed the set; no implementation or release has been accepted |

The first three are designed in documentation. The owner-approved documentation baseline is a separate sign-off; executed evidence, independent review and implementation sign-off are absent. A milestone cannot exit on the strength of planned cases alone.

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
