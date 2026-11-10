# Experience

Version 0.5 | November 2026 | Status: shared documentation section and coverage index.

What the numbers mean on screen, where a user moves between screens, and who may act on what.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Dashboard and operations](dashboard-and-operations.md) | Four tiles and the rules that stop misreading, granularity and time range, the two surfaces, the role and capability matrix, the operation safety model, capture policy screens, empty states |
| [Wireframes and navigation](wireframes-and-navigation.md) | The two-surface navigation model, nine routes, five screen layouts and the operation dialog |
| [Screenshot index](screenshot-index.md) | Every screen and dialog the prototype has, 35 images captured from one build, with the wireframe section each illustrates |
| [Accessibility requirements](accessibility.md) | NFR-008 in full: keyboard, semantics, contrast, charts, time limits, target size, and what an automated audit cannot prove |
| [Visual design tokens](visual-design.md) | Colour, type, spacing, radius and elevation, with contrast ratios computed from the tokens; the component inventory and its states |
| [Role and capability matrix](dashboard-and-operations.md) (§2) | Three roles against thirteen capability rows; Viewer sees the reporting dashboard only (DEC-002) |
| [Operation coverage matrix](../13-traceability/operation-coverage-matrix.md) | Which operation serves which requirement, role and milestone |

## The rules that prevent the worst failure

A monitoring product's most damaging output is a confident zero. Four rules in §1 and §4 exist to prevent it:

| Situation | Must never show |
| --- | --- |
| Queue has no dead-letter routing | `0` dead letters; show "monitoring unavailable" |
| Capture has stopped while depth grows | `0` messages; show a stale badge |
| Storage ceiling reached, rows paused | `0` messages; show "capped" while counts continue |
| Granularity not retained for that range | An empty chart; refuse the request |

The same rule is why status is never colour-only: a stale queue that looks like a green dot has failed twice, once for the operator and once for a screen reader ([accessibility §2.2](accessibility.md)).

## Relationship to other sections

This section owns presentation and permission, not the numbers. Metric definitions live in [data-model.md](../07-data-and-contracts/data-model.md) §3; if a meaning changes, it changes there first.

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
