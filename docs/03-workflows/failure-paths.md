# Failure paths

Version 0.1 | 09 November 2026 | Status: Draft

The three lifecycles describe what happens when things work. This document draws what happens when they do not, because a failure path that exists only as a table row is a failure path nobody has looked at end to end.

Diagrams are inline Mermaid. They sequence behaviour; they introduce no rule. The owning documents are cited on each.

## 1. Capture: the three ways a queue stops being observed

Three failures look similar on a dashboard and have nothing in common in their cause. The diagram exists to make the branch explicit — which state you are in decides who fixes it.

```mermaid
flowchart TD
    A[Capture enabled for a queue] --> B{Topology write permitted?}
    B -- no --> U[captureState: unavailable<br/>name the missing grant<br/>ERR-BRK-001]
    B -- yes --> C[Shadow queue declared and bound]
    C --> D{Worker consuming?}
    D -- no --> S1[captureState: stale<br/>last-seen timestamp<br/>depth rising beside it]
    D -- yes --> E{Copies being dropped?}
    E -- yes --> G[Capture-gap warning<br/>observed vs broker publish counter]
    E -- no --> F{Storage ceiling reached?}
    F -- yes --> P[captureState: capped<br/>rows pause, rollups continue]
    F -- no --> L[captureState: live]
    S1 --> R1[Fix: worker, database or network]
    G --> R2[Fix: raise x-max-length or narrow the binding]
    P --> R3[Fix: ceiling, retention or payload policy]
    U --> R4[Fix: grant, or pre-create the queue]
```

| State | Who fixes it | What must never be shown |
|---|---|---|
| `unavailable` | The adopter's platform team — a permission | A queue that looks healthy with no capture |
| `stale` | Product or environment — see [runbook §5](../11-operations/runbook.md) | A frozen count presented as current |
| `capped` | The adopter — a storage decision | Zero traffic; counts are still accruing |
| Capture gap | The adopter — the shadow queue's bound | An "exact" label on a number known to be short |

Owned by [capture lifecycle](capture-lifecycle.md) §3 and [failure modes](../05-architecture/failure-modes.md) §2–§5.

## 2. Dead-letter blind spot: when the count is honest and useless

The one failure with no clean fix. A queue with no dead-letter routing loses failures before Marites can see them, so the dead-letter figure is unavailable rather than zero — and unavailable is true but uninformative.

```mermaid
flowchart LR
    P[Message published] --> Q[Business queue]
    Q --> C{Consumer rejects}
    C -- with DLX --> X[Dead-letter exchange]
    X --> D[DLQ] --> M[Marites captures<br/>reason recorded]
    C -- no DLX --> L[Message discarded by the broker]
    L --> N[No row, no reason, no count]
    N --> G[Only signal: published exceeds delivered]
    M --> R[deadLetterConfigured: true]
    N --> S[deadLetterConfigured: false<br/>monitoring unavailable]
```

`deadLetterConfigured: false` is the honest label; the published-versus-delivered gap is the only remaining signal, and it is tracked under FR-018. Neither is a substitute for the adopter configuring a dead-letter exchange ([DEC-004](../01-project/decisions-and-open-questions.md)).

## 3. Recovery: where the loop breaks

The loop is Detect → Understand → Act → Prove. Each arrow below is a place it can stop.

```mermaid
flowchart TD
    D[Detect: depth and dead letters] -->|capture stale| D1[Counts frozen, depth rising]
    D --> U[Understand: reason breakdown]
    U -->|no payload stored| U1[Reason known, body absent<br/>policy outcome, not an error]
    U --> A[Act: replay / discard / publish]
    A -->|dry run shows same reason| A1[Replay would dead-letter again<br/>fix the consumer first]
    A -->|target grows faster| A2[Job auto-halts<br/>human resume required]
    A -->|fails partway| A3[202 Unresolved<br/>reconcile, do not retry]
    A --> P[Prove: audit row]
    P -->|row missing| P1[Operation never executed<br/>or a defect]
    D1 --> X[Loop cannot start<br/>fix capture first]
    U1 --> A
```

| Break | The wrong response | The right response |
|---|---|---|
| Counts frozen | Replay from memory | Fix capture; a replay aimed by a stale count is aimed blind |
| Body absent | Report a bug | Read the policy; `sampled` and `none` are choices |
| Dry run shows the same reason | Press replay anyway | Fix the consumer, then replay |
| Auto-halt | Resume immediately | The condition has not changed; it will halt again |
| `Unresolved` | Retry | Poll the operation record and reconcile |

Owned by [recovery lifecycle](recovery-lifecycle.md) §2–§4 and [DOC-UX-001 §3](../08-experience/dashboard-and-operations.md).

## 4. Onboarding: the three exits

```mermaid
flowchart TD
    S[Add broker profile] --> P[Preflight]
    P -->|management unreachable| X1[Hard stop<br/>enable the plugin or choose another provider]
    P -->|topology write refused| X2[Hard stop for capture<br/>grant, or pre-create the queue<br/>live console still available]
    P -->|passes| Q[Select queues]
    Q --> B[Shadow queue bound]
    B --> L[Live]
    P -->|no dead-letter exchange| W[Onboarding succeeds<br/>dead-letter monitoring unavailable]
    W --> L
```

Two of the three exits are hard stops and one is a degraded success. The distinction matters commercially: an adopter whose queues have no dead-letter exchange still gets volume, inspection and replay — only the failure reason is missing ([onboarding lifecycle](onboarding-lifecycle.md) §3).

## 5. What these diagrams do not show

- **Timing.** No timeouts, thresholds or retry intervals are drawn, because none are set ([failure modes §10](../05-architecture/failure-modes.md)).
- **Multi-cluster.** A failure in one profile does not propagate, but the diagrams show one profile ([architecture overview](../05-architecture/architecture-overview.md) §10).
- **Broker-side failure.** If the broker loses messages, every path above reports what Marites observed, not what happened.

## 6. References

- States and their meanings: [VOCABULARY §15](../14-reference/vocabulary.md)
- Component behaviour per failure: [failure modes](../05-architecture/failure-modes.md)
- Operator response: [runbook](../11-operations/runbook.md)
- Error outcomes: [error catalog](../07-data-and-contracts/error-catalog.md)
