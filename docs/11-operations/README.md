# Operations

Version 0.5 | November 2026 | Status: Approved — Product owner, 2026-11-11

What an adopter must supply, how Marites deploys, what ships when, and what to do when it goes wrong.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Deployment and onboarding](deployment-and-onboarding.md) | RabbitMQ prerequisites (§1), preflight (§2), deployment shapes (§3), broker profiles (§4), what Marites creates in a broker (§4.1), first run (§5), profiles and milestones M0 to M7 (§6) |
| [Provider prerequisites](provider-prerequisites.md) | Service Bus and Pub/Sub identities, the exact rights, the commands that verify them, and the preflight mapping |
| [Storage prerequisites](storage-prerequisites.md) | Which engine to choose, the two database roles, the grants per engine, Azure SQL's four differences, and what Marites must never be given |
| [Upgrade and rollback](upgrade-and-rollback.md) | What a release is, the order of operations, four rollback scenarios, and the five actions that cannot be reversed |
| [Backup and recovery](backup-and-recovery.md) | What must be backed up, what cannot be recovered, proposed RPO/RTO, the restore procedure and its verification checks |
| [Configuration reference](configuration-reference.md) | Every `MARITES_*` variable with type, default and effect; what belongs in the environment and what must never; startup validation |
| [Runbook](runbook.md) | Eighteen symptoms with a first check and an action, the diagnostics that separate a product defect from an environment problem, and six things not to do |
| [Onboarding lifecycle](../03-workflows/onboarding-lifecycle.md) | The adopter's path and the three ways it stops |
| [Assumptions and risks](../01-project/assumptions-and-risks.md) | Broker and throughput assumptions that gate the milestones |

## The section most likely to be skipped, and least able to be

An adopter who does not enable the management plugin gets an application that looks broken. An adopter whose queues have no dead-letter exchange gets a product that cannot report failures. Neither is a bug, and neither is obvious.

§1.7 is the one to read before a compliance conversation: **Marites stores payloads exactly as published.** If that is unacceptable, `payload_policy = none` still delivers exact counts, but the conversation should happen before go-live.

## The four numbers worth knowing before an incident

| Number | Where it is | Why |
|---|---|---|
| Retention: 72 hours of rows, 3 years of counts | [Data model §4](../07-data-and-contracts/data-model.md) | An operator who expects 90 days of messages will report a bug that is a setting |
| **Evidence: the longer of the row retention and the evidence setting** — 90 days by default, settable to 365 | [DEC-017](../01-project/decisions-and-open-questions.md) | The one place a payload can outlive its message, on purpose. An operator who finds a purged body from August on a 3-day queue will otherwise report a retention bug that is the design |
| Ceiling: `min(50 GB, 20% of disk)` by default | [Data model §4.1](../07-data-and-contracts/data-model.md) | Reaching it stops rows and keeps counts. It must never read as zero traffic |
| Backup: proposed 1 hour of counts, 4 hours to usable | [Backup and recovery §3](backup-and-recovery.md) | Not agreed with anyone; proposed, and stated as such |

## Milestones at a glance

| | Delivers |
| --- | --- |
| M0 | Contract, skeleton, preflight, deployable shell |
| M1 | Metadata dashboard and live support console; Support can complete a recovery |
| M2 | Capture pipeline: counts and history |
| M3 | Message explorer, captured-message replay and publish |
| M4 | Multi-broker and tenancy |
| M5 to M6 | Service Bus, then Pub/Sub |

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
