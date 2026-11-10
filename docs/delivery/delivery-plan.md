---
document_id: DOC-DEL-002
title: Delivery Plan
product: Marites
---

# Delivery plan

The [readiness dashboard](readiness-dashboard.md) says where the project *is*. This says what the *work* is: the packages, what each one finishes, and what it waits for. The two are different questions, and the set only answered the first.

**Nothing here is scheduled.** There are no dates and no estimates — a plan with invented dates is a plan nobody trusts after the second slip. The order is real; the calendar is not.

## 1. Milestones and work packages

The milestone names and exit criteria are owned by [deployment and onboarding §6](../operations/deployment-and-onboarding.md) and are not restated here; each ends at a gate ([verification plan §3](../testing/verification-plan.md)). What this document adds is the **packages** beneath them — a package is a unit of work that can be reviewed on its own.

### M0 — Contract, skeleton, deploy

| Package | Finishes when |
|---|---|
| `WP-M0-1` Contract freeze | `openapi.yaml` validates in CI and every operation is tagged; changes after this point require a decision record |
| `WP-M0-2` Skeleton | Three entrypoints (`api`, `worker`, `all`), the three ports, and the architecture test that enforces the reference rules |
| `WP-M0-3` Bookkeeping store | SQLite file created at install, migrations applied, audit table writable |
| `WP-M0-4` Identity | Entra JWT validated, role→capability map enforced, `ERR-AUTH-001/002` returned |
| `WP-M0-5` Capabilities | `/capabilities` served from the provider record; `can_*` flags drive the UI |
| `WP-M0-6` Preflight | Every check in the preflight set runs and names its remedy on failure |
| `WP-M0-7` Wizard | The setup wizard blocks a save on a failing check |
| `WP-M0-8` Deploy | `docker compose up` on a clean host yields a working UI against a test broker; health and metrics endpoints live |
| `WP-M0-9` Harness v0 | Producer and rejecting consumer run against RabbitMQ; one scenario (`poison-subset`) produces a manifest |

### M1 — Live support console

| Package | Finishes when |
|---|---|
| `WP-M1-1` RabbitMQ adapter | Admin and message operations against a real broker, behind `IBrokerAdmin`/`IMessageTap` |
| `WP-M1-2` Live operations | Peek, discard, purge and redrive act on the broker, with dry runs and typed confirmations |
| `WP-M1-3` Hold and promote | `operation_intent` written before the command, promoted on confirmation, reconciled on restart (ADR-023) |
| `WP-M1-4` Audit and evidence | Every operation writes its row and its bodies; the audit screen and payload dialog read them |
| `WP-M1-5` Console UI | Queue list, queue detail, the action bar, the four dialogs, the audit screen |

**M1 is the milestone that makes the product real.** A Support user completes a recovery without leaving the UI, and the audit proves it afterwards.

### M2 — Capture pipeline

| Package | Finishes when |
|---|---|
| `WP-M2-1` Shadow binding | Capture reads every message crossing the exchange, dead-letter or not (ADR-003) |
| `WP-M2-2` Batcher and writer | Binary `COPY` at 5,000 msg/s, with the redelivery ledger in the same transaction (ADR-019) |
| `WP-M2-3` Rollups | Exact counts per interval, never rebuilt from rows (ADR-007) |
| `WP-M2-4` Partition lifecycle | Daily partitions created ahead, dropped on retention |
| `WP-M2-5` Ceiling | `ERR-QUE-003` refuses an over-ceiling policy; the sweeper pauses at the ceiling |
| `WP-M2-6` Storage port | PostgreSQL implementation; the conformance suite defines "supported" (ADR-022) |

### M3 — Explorer, historical actions, agents

| Package | Finishes when |
|---|---|
| `WP-M3-1` Search | Filters, paging, duplicate grouping over `message_capture` |
| `WP-M3-2` Historical replay and publish | Replay a captured message, edit a body, publish a new one |
| `WP-M3-3` MCP server | Read, act and propose tools, filtered by the credential (ADR-021) |
| `WP-M3-4` Chat drawer | The same registry, hosted in-product, with the payload switch |

### M4 — Multi-broker and tenancy

`WP-M4-1` bulkhead isolation across clusters · `WP-M4-2` environment-scoped confirmations · `WP-M4-3` row-level tenant policy and isolation tests.

### M5 — Azure Service Bus · M6 — Google Pub/Sub

`WP-M5-1` / `WP-M6-1`: an adapter passing the shared conformance suite at `full` tier. Neither is a "provider screen" — the baseline screens are the product (ADR-017).

### M7 — SQL Server message store

`WP-M7-1` the second storage-port implementation. **This milestone was referenced by ADR-022 and the storage prerequisites but was missing from the milestone table** — recorded here so it has an owner and a place in the order.

## 2. Definition of done

A package is not done because the code merges. It is done when all five hold:

1. **The behaviour is specified** — an FR or an ADR says what it should do, and if it does not, the document is written first.
2. **Its cases pass** — the TC cases the criterion cites are executed, not planned.
3. **The failure path exists** — every new operation has an error code, and every new error has a row in the [catalogue](../data-and-contracts/error-catalog.md).
4. **The documents agree** — no other document still describes the old behaviour. This is the rule the review keeps having to enforce, so it is stated as part of done.
5. **Nothing is claimed that was not run** — a package may ship with a known gap, named in the dashboard, but not with a green tick it did not earn.

## 3. What blocks what

| Milestone | Waits for | Because |
|---|---|---|
| M1 | M0 | There is no audit without the bookkeeping store, and no operation without an audit |
| M2 | M0 | Capture has nowhere to write until the message store exists |
| M3 | M1, M2 | Historical actions read the capture; replay reuses the M1 executor |
| M4 | M1 | Isolation is a property of operations that already work |
| M5, M6 | M2 | An adapter is only "supported" when it passes the capture conformance suite |
| M7 | M2 | The port is defined by its first implementation, not by the second |

**M3 is the only place where two dependencies meet**, and it is where a slip in either M1 or M2 becomes visible. That is deliberate: the agent work is the least sequential part of the product and the best place to absorb a delay.

## 4. Not scheduled

Named so they are not mistaken for omissions:

| Gap | Why it is not scheduled |
|---|---|
| Chaos and soak | Needs a stable M2 and a decision about what "soak" means for a self-hosted tool |
| Object store for bodies over 64 KB | Open (RSK-013); truncation is the current, stated behaviour |
| Storage-usage gauge | Deliberate omission, with a trigger to revisit ([storage prerequisites §7](../operations/storage-prerequisites.md)) |
| Evidence retention floor for destructive operations | Undecided. Today evidence dies with the capture window |
| Role-assignment UI | `CanAssignRoles` exists with no screen; identity is Entra's |

## 5. How this plan is tracked

Each package becomes one issue, with the milestone as its GitHub milestone and the package id in the title. The mapping, labels and board are in [the GitHub project](github-project.md). This document stays the source of truth for *what* the packages are; the board tracks *how they are going*.

## 6. Document control

Revision 0.1, 10 November 2026. The milestone table is taken from [deployment and onboarding §6](../operations/deployment-and-onboarding.md), which owned it; the work packages, the definition of done and the dependency table are new. M7 is added because two documents already assumed it. No date, estimate or staffing assumption appears anywhere in this document, deliberately.
