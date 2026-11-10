# Delivery management

Version 0.3 | 10 November 2026 | Status: Partial; readiness, review and the delivery plan written; no code.

Keep the documentation baseline, milestone ownership and review evidence traceable.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Readiness dashboard](readiness-dashboard.md) | Single summary of readiness by layer, the M0 exit gate, and the four items that would change the page most |
| [Documentation review](documentation-review.md) | Three self-review passes: 07 November 2026 (AUD-001 to AUD-025), 09 November 2026 completion and 10 November 2026 UI review (AUD-026 to AUD-069), with method, resolutions and the checks run |
| [Verification plan](../testing/verification-plan.md) (§3) | The eight milestone gates that decide whether a milestone exits |
| [Delivery plan](delivery-plan.md) | Work packages `WP-M0-1` onward, the definition of done, what blocks what, and what is deliberately not scheduled. Milestone names stay owned by operations; M7 is added here because two documents already assumed it |
| [CI and release](ci-and-release.md) | What runs on a pull request versus a tag, the ten gates, versioning and migrations, artifacts, and the release checklist |
| [GitHub project](github-project.md) | Repository layout, three label axes, four issue templates, project fields, the small set of automations, and branch and PR conventions |
| [Milestones M0 to M7](../operations/deployment-and-onboarding.md) (§6) | Owned by operations; this section links and never restates them |
| [Documentation changelog](../project/documentation-changelog.md) | Baselines 0.1 to 0.19 |
| [Assumptions and risks](../project/assumptions-and-risks.md) | ASM and RSK records that gate the milestones |

## The distinction this section exists to keep

| Category | Present | Evidence |
| --- | --- | --- |
| Requirement | Yes | 28 FR, 8 NFR with acceptance criteria |
| Technical proposal | Yes | 23 ADRs (22 accepted, 1 superseded), 19 decisions, twelve tables, a contract |
| Designed verification | Yes | 28 cases, 8 gates, all `Planned` |
| Executed evidence | **No** | No test has run |
| Sign-off | **No** | No second party has approved anything |

Most of the five are absent or partial, and this table is the only place that says so in one glance. A milestone cannot exit on the strength of the third row.

## Work still needed

- **No repository, issue tracker or board.** Delivery work is tracked in these documents and nowhere else.
- **No CI pipeline exists.** The ten release gates are specified, but none is implemented or running.
- **M0 has unmet exit criteria.** No skeleton, no `docker compose up`, no served `/capabilities` or preflight.
- **No release record.** Nothing has been tagged, so [upgrade and rollback](../operations/upgrade-and-rollback.md) describes a procedure with no history behind it.

Use the [documentation structure](../project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
