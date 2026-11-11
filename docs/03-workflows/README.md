# Workflows

Version 0.3 | November 2026 | Status: Approved — Product owner, 2026-11-11

The paths this system actually has: a message's, an operator's, an adopter's, and two that describe what happens when they break.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Capture lifecycle](capture-lifecycle.md) | Publish, fan-out, shadow copy, normalise, store, count and ack, the dead-letter branch, and where it stops working |
| [Recovery lifecycle](recovery-lifecycle.md) | Detect, understand, act, prove: the loop Support runs, the guard chain, where the loop breaks, and what cannot be undone |
| [Onboarding lifecycle](onboarding-lifecycle.md) | Register, preflight, select, capture, live, and the three ways it stops |
| [Failure paths](failure-paths.md) | Four diagrams: the three ways capture stops, the dead-letter blind spot, where the recovery loop breaks, and the three onboarding exits |
| [Retention and compaction lifecycle](retention-lifecycle.md) | Tier lifecycle, one sweeper pass in order, the ceiling state machine, and the six ways it can still go wrong |

Workflow diagrams are maintained as editable JSON sources with PNG previews for inline Markdown display. These are design specifications; the project has no implemented runtime.

## Diagram sources

| Diagram | Type | JSON source | Inline preview |
| --- | --- | --- | --- |
| Live capture | Sequence | [source](diagrams/capture-live.sequence.json) | [PNG](diagrams/capture-live.png) |
| Dead-letter capture | Sequence | [source](diagrams/dead-letter-capture.sequence.json) | [PNG](diagrams/dead-letter-capture.png) |
| Recovery actions | Sequence | [source](diagrams/recovery-actions.sequence.json) | [PNG](diagrams/recovery-actions.png) |
| Onboarding setup | Sequence | [source](diagrams/onboarding-setup.sequence.json) | [PNG](diagrams/onboarding-setup.png) |
| Capture failure states | Workflow | [source](diagrams/capture-failure-states.workflow.json) | [PNG](diagrams/capture-failure-states.png) |
| Dead-letter blind spot | Workflow | [source](diagrams/dead-letter-observability.workflow.json) | [PNG](diagrams/dead-letter-observability.png) |
| Recovery breaks | Workflow | [source](diagrams/recovery-breaks.workflow.json) | [PNG](diagrams/recovery-breaks.png) |
| Onboarding exits | Workflow | [source](diagrams/onboarding-exits.workflow.json) | [PNG](diagrams/onboarding-exits.png) |
| Rollup tiers | Lifecycle | [source](diagrams/rollup-tiers.lifecycle.json) | [PNG](diagrams/rollup-tiers.png) |
| Retention sweeper pass | Sequence | [source](diagrams/retention-sweeper.sequence.json) | [PNG](diagrams/retention-sweeper.png) |
| Storage ceiling | Lifecycle | [source](diagrams/storage-ceiling.lifecycle.json) | [PNG](diagrams/storage-ceiling.png) |

Rebuild the inline previews from the repository root with the shared diagram builder:

```bash
python docs/05-architecture/diagrams/build.py docs/03-workflows/diagrams/*.sequence.json docs/03-workflows/diagrams/*.workflow.json docs/03-workflows/diagrams/*.lifecycle.json
```

See [architecture diagrams](../05-architecture/diagrams/README.md) for build prerequisites and details about interactive HTML views, which are local build products and not committed.

## Reading order within this section

1. **Capture** first: it explains why the product can see anything at all.
2. **Recovery** second: it explains what the product is for.
3. **Onboarding** third: it explains what has to be true before either of the others can happen.
4. **Failure paths** fourth: the same three, entered from the branch nobody draws.
5. **Retention** last: what happens to the data after every other path has finished with it.

## Relationship to other sections

Workflows sequence behaviour; they do not own it, and they introduce no new rules.

| Question | Owner |
| --- | --- |
| What must the system do? | [business/requirements.md](../02-business/requirements.md) |
| Why is the tap one hop? | [architecture/architecture-overview.md](../05-architecture/architecture-overview.md) §3 |
| What does each component do when it fails? | [architecture/failure-modes.md](../05-architecture/failure-modes.md) |
| What is stored and what does a count mean? | [data-and-contracts/data-model.md](../07-data-and-contracts/data-model.md) |
| What does the operator see and confirm? | [experience/dashboard-and-operations.md](../08-experience/dashboard-and-operations.md) |
| What should an operator do about it? | [operations/runbook.md](../11-operations/runbook.md) |

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
