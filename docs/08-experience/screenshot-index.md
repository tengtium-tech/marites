---
document_id: DOC-UX-005
title: Screenshot Index
product: Marites
status: Approved — Product owner, 2026-11-11
version: 0.1
date: 2026-11
---

# Screenshot index

Thirty-nine static screenshots from the UI exploration, with the wireframe section each one illustrates. They are retained as design references, not as a runnable application.

**The figures are embedded where they are read** — in the [wireframes](wireframes-and-navigation.md), one set under each section. This document is the map: it says what each file shows, which section it belongs to, and what was deliberately left uncaptured.

**The images are design references, not evidence of the product.** Where a screenshot and the wireframes disagree, the wireframes win; the specification remains authoritative.

## 1. Source and limits

These screenshots were retained from the UI exploration after its source application and capture script were removed from the repository. They illustrate one explored implementation and are not evidence of a production build or a reproducible current application.

| Property | Value |
|---|---|
| Viewport | 1440 × 1000 (1180 for the tall audit and queue screens) |
| Scale | 2× — readable when cropped for a document |
| Budget | 8 s of virtual time, so the theme effect and the fade-in have settled |
| Source | One build, so no two images disagree about the current design |

## 2. The set

### Sign in

| File | Shows | Wireframes |
|---|---|---|
| `00-login.png` | Three demo accounts, the email form, the Entra caveat | §5 |
| `00-login-dark.png` | The same, dark — the tokens, not a second palette | §5 |

### Queues and the support console

| File | Shows | Wireframes |
|---|---|---|
| `01-queues.png` | Queue list: depth, rate, dead letters, `captureState`, and the two banners | §3.1 |
| `01b-queues-filter-dlq.png` | The table filtered to the queues with no dead-letter routing | §3.1 |
| `01c-queues-filter-capture.png` | The table filtered to the stopped and capped queues | §3.1 |
| `02-queue-detail.png` | Metric strip, the action bar in the table header, capture policy, reason breakdown | §3.6 |
| `14b-live-only.png` | Live-only: the banner, the empty history state, the capture card reading *off* | §3.10 |
| `14c-live-only-queues.png` | The queue list in live-only: capture reads `off` everywhere, and neither warning banner applies | §3.1 |
| `06-dark.png` | The queue detail in dark | §5 |

### Dialogs

| File | Shows | Wireframes |
|---|---|---|
| `07-peek.png` | Live messages, the redelivered flag, the get-and-requeue caveat | §3.4 |
| `03-replay-dialog.png` | The selection, an editable body, the validity check, the typed confirmation | §3.6 |
| `08-discard.png` | Every body listed before the command, each openable, plus the typed name | §3.7 |
| `15-purge.png` | Two typed fields — queue name and environment — with the button disabled | §3.6 |
| `10-agent-executed.png` | An assistant's replay already done, attributed, with no approval step | §3.8 |

### The payload dialog

| File | Shows | Wireframes |
|---|---|---|
| `11d-payload-diff.png` | Before and replayed side by side, line numbers, the changed line marked `−` and `+` | §3.11.1 |
| `11e-payload-discard.png` | One pane, the body that was destroyed | §3.11.1 |
| `11f-payload-purge.png` | The same dialog opened from one row of a purge | §3.11.1 |
| `15c-payload-publish.png` | The body a publish sent | §3.11.1 |
| `08b-discard-payload.png` | Preview before the command: no hash, no retention date yet | §3.7 |
| `11g-payload-dark.png` | The same diff, dark — tint and markers both survive | §3.11.1 |

### Setup wizard

| File | Shows | Wireframes |
|---|---|---|
| `04-setup-profile.png` | Provider, endpoint, credential kind, and what enabling capture will create | §3.5 |
| `04a-setup-storage.png` | The message store — optional, and what skipping costs | §3.5, §3.10 |
| `04b-setup-preflight.png` | One row per check, a failing check naming its remedy | §3.5 |
| `04c-setup-queues.png` | Queues pre-selected by dead-letter routing | §3.5 |
| `04d-setup-capture.png` | What will be watched, the ceiling, the backfill offer | §3.5 |

### Publish, reporting, audit

| File | Shows | Wireframes |
|---|---|---|
| `09-publish.png` | Composer, target summary, payload hash, typed environment | §3.6 |
| `05-reporting.png` | Four tiles, the trend, the per-queue table | §4 |
| `14d-live-only-report.png` | Reporting with no history: em-dash tiles and an explanation instead of a chart | §4 |
| `11-audit.png` | Human actions and assistant actions side by side, `via Claude` on the executed one | §3.11 |
| `11b-audit-payload.png` | A single replay expanded: the body whole, before and after | §3.11 |
| `11c-audit-bulk.png` | A purge of ten expanded: ten rows, one per message | §3.11 |
| `11h-audit-expired.png` | Evidence past retention: when it was swept, which retention applied, the hash that remains | §3.11 |

### Storage

| File | Shows | Wireframes |
|---|---|---|
| `14-storage.png` | Connected: engine, host, schema, both roles, and what the store holds — and does not | §3.10 |
| `15b-storage-add.png` | Live-only: the form, what turns on, what is already on | §3.10 |

### The assistant

| File | Shows | Wireframes |
|---|---|---|
| `12-ai-settings.png` | Provider, base URL, model, the payload switch, what leaves the building | §3.9 |
| `13-chat-drawer.png` | The drawer open, three suggested questions | §3.9 |
| `13b-chat-answer.png` | An answer with an evidence table and a completed action card | §3.9 |
| `13c-chat-not-configured.png` | No model connected — the explanation, and who can fix it | §3.9 |
| `13d-chat-not-configured-admin.png` | The same state for an Admin, with the fix in it | §3.9 |

## 3. What is deliberately not captured

| Not captured | Why |
|---|---|
| A **proposal** on the queue screen | There is no separate screenshot for this state. The propose path is described where it leaves a permanent trace: the `proposal` row in `11-audit.png` |
| The **replace storage** confirmation | Same screen as `15b`, one step further on; the copy is asserted in §3.10 and a screenshot would not add to it |
| **Error and empty states** beyond live-only | Not designed yet. Capturing them would present a design decision that has not been made |
| **Mobile** | The product is a desktop console; the narrow layout exists only so nothing breaks, not as a target |

## 4. Document control

Revision 0.1 records the 39 static screenshots retained as design references. The original UI exploration and its capture script are not part of the repository.
