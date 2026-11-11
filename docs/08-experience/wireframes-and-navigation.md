---
document_id: DOC-UX-003
title: Wireframes and Navigation
product: Marites
version: 0.7
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-UX-003 — Wireframes and Navigation

**Source:** [dashboard and operations](dashboard-and-operations.md) §1.1, §3, §4; [API catalog](../07-data-and-contracts/api-catalog.md). Owning document for layout and navigation. Metric meaning is owned by DOC-UX-001 §1; this document does not restate it.

**The figures below are static screenshots of the UI exploration.** They illustrate intended layouts; the rules under each figure define behavior and constraints. Colour, type and spacing are owned by [visual design](visual-design.md).

What an image cannot carry is the **rules**: why a control sits where it does, what it refuses, and what it says when it cannot do the thing. Those are the tables under each figure, and they are the part worth reviewing in a pull request. A mockup beside a photograph is the same information twice, and the mockup is the one that goes stale — so there are none left in this document.

## 1. Navigation model

Two surfaces that share no layout ([DOC-UX-001 §1.1](dashboard-and-operations.md)). One product, two front doors, chosen by role.

| Role | Lands on | Reaches |
|---|---|---|
| Viewer | `/report` | The reporting surface, and nothing else |
| Support | `/queues` | The operations console: queues, publish, audit, and the assistant |
| Admin | `/queues` | The console, plus brokers, setup, storage, purge and the AI provider settings |

`/login` sits in front of both, and is described in §5 — it is the one screen Marites owns rather than delegates.

| Viewer | Support · Admin |
|---|---|
| ![05-reporting](images/05-reporting.png) | ![01-queues](images/01-queues.png) |
| *Four tiles, a trend, a per-queue table. No message bodies, no operation controls, no queue a Viewer can act on.* | *Depth, rates, dead letters and capture state, with the two banners that appear when something needs attention.* |

| Rule | Why |
|---|---|
| The landing route redirects by role; there is no shared home page | A Viewer who lands on a queue list sees a surface built for someone else, and a Support user who lands on a chart has to navigate away before starting work |
| A Support or Admin user can open the reporting view, but it is a link, not the default | The executive view is a different question, and it is occasionally useful to the same person |
| A Viewer cannot reach an operations route at all — the routes are not rendered, and the API would refuse them | Hiding a control the API refuses is a courtesy; the API refusal is the control (DEC-002) |
| The broker switcher is global and always visible on the console | Every figure is scoped to a profile; a number without its cluster is ambiguous, and `orders` exists in two of them (VOCABULARY §10) |

## 2. Route map

| Route | Surface | Screen | Contract |
|---|---|---|---|
| `/login` | Both | Sign-in, or the break-glass form (§5) | OIDC redirect; `POST /auth/break-glass` |
| `/setup` | Console | First-run wizard | `POST /brokers`, `POST /brokers/{id}/preflight` |
| `/queues` | Console | Queue tree and list | `GET /queues` |
| `/queues/:id` | Console | Queue detail, capture policy | `GET /queues/{id}`, `PUT /queues/{id}/capture-policy` |
| `/queues/:id/messages` | Console | Message list — the table inside the queue detail (§3.3) | `QUERY /messages` |
| `/messages/:id` | Console | Message detail — the read-only payload dialog (§3.11.1) | `GET /messages/{id}` |
| `/operations/:id` | Console | Operation progress | `GET /operations/{id}` |
| `/brokers` | Console | Broker profiles | `GET /brokers`, `GET /brokers/{id}/capabilities` |
| `/publish` | Console | Message composer | `POST /operations/publish` |
| `/storage` | Console | Database settings: add later, test, replace (Admin) | `PUT /storage` |
| `/ai` | Console | AI provider settings (Admin) | `PUT /ai/provider` |
| *(drawer)* | Console | Assistant panel over any screen | `POST /ai/chat` |
| `/audit` | Console | Audit log, including agent proposals | `GET /audit` |
| `/report` | Reporting | Volume and dead-letter dashboard | `GET /metrics/*` |

**Every route maps to contract operations, and nothing else.** A screen that needs data the contract does not serve is a contract gap, not a frontend decision.

## 3. Operations console

### 3.1 Queue list (`/queues`)

One row per queue, five numbers, and a banner whenever one of them needs attention. The screen answers *which queue is the problem* before it answers anything else.

| Element | Behaviour |
|---|---|
| Broker switcher | Changes the profile for every figure on the screen. Never mixed: one screen, one profile |
| Banners | Two, and they are different problems. *"2 queues cannot report failures"* is a **configuration** gap — those queues will never show a dead-letter count, and waiting does not fix it. *"1 capture has stopped · 1 capped"* is a **state** problem, and the two halves have opposite consequences: a stopped capture freezes the figures, while a capped one keeps counting and stops storing. Saying "both are old" would be false for the second, and the footnote below the table says so. Both name the count, and neither appears when the count is zero |
| **A banner that names a count must be able to show it** | `View` filters the table to the queues the banner counted, and turns into `Show all`. A chip reads *"Showing 2 of 5 — no dead-letter routing"* with a clear action. A summary with no way to reach its subject is a dead end, and it is also unverifiable: the operator cannot check that the count is right |
| **Status appears when it changes, not permanently** | A green "capture health: passing" badge that sits in the sidebar all day is a badge people stop reading — and this one contradicted itself, showing *passing* while listing one stale and one capped queue. The state belongs in a banner that appears when it is true, and in the queue's own row |
| Watched count | *"5 queues watched"* is in the page header, in words. It was previously a permanent counter in the sidebar, which is where a number goes when nobody has decided what it is for |
| Dead-letter column | Shows `unavailable` where there is no routing, never `0` (`ERR-QUE-002`) |
| Capture column | The five states of VOCABULARY §15, each with a distinct icon **and** a word |
| **Live-only changes the column, not just the banner** | With no message store, capture is not running — so every queue reads `off`, both banners are absent, and the footnote explains `off` instead of `stale`/`capped`. Showing `live` beside a live-only banner would claim a state the install cannot be in |
| Row | Opens the queue detail. Rows are keyboard-reachable and the whole row is the target |

![01-queues](images/01-queues.png)

*The queue list. Depth, rate and dead letters per queue, `captureState` as a word beside the number rather than a colour, and the two banners that appear when something needs attention — never a permanent status badge.*

![01b-queues-filter-dlq](images/01b-queues-filter-dlq.png)

*`View` on the first banner: the table filtered to the two queues that cannot report failures, the count in the chip, and the link turned into `Show all`.*

![01c-queues-filter-capture](images/01c-queues-filter-capture.png)

*`View` on the second: the stopped queue and the capped one, side by side — which is where the difference between them is visible, since one has frozen figures and the other is still counting.*

![14c-live-only-queues](images/14c-live-only-queues.png)

*The same list in live-only. The banner appears where the gap is felt and says what still works — recovery and the audit. The capture column reads `off` for every queue and both warning banners are absent, because capture is not running at all.*

### 3.2 Queue detail (`/queues/:id`)

The metric strip answers *how bad*, the reason breakdown answers *why*, and the action bar sits on the same line as the selection that enables it — so an operator never has to look in two places to find out what a button will act on.

| Element | Behaviour |
|---|---|
| Action bar | Each button is present only when `ProviderCapabilities` allows it. Absent, not disabled — a disabled button with no reason is a support ticket |
| Replay | Opens the dry-run dialog (§3.4). Never executes directly |
| Purge | Admin only, typed queue name, environment tag on `prod` |
| Capture policy | Read-only summary with the non-default marker where a value was changed |

![02-queue-detail](images/02-queue-detail.png)

*The queue detail. The metric strip, the action bar sitting on the same line as the checkboxes that enable it, the capture policy, and the reason breakdown.*

### 3.3 Message list and detail

**This is the table inside the queue detail, not a second screen.** One table over one set of rows, filtered — a separate message-list page would be the same data with a different header, and two places to fix a column. Opening a row is the read-only payload dialog (§3.11.1), which is where a captured body is actually read.

| Element | Behaviour |
|---|---|
| Filter row | Filters combine conjunctively; changing any filter discards the paging cursor |
| Duplicate grouping | A count and a hash prefix, expanding to the individual messages. This is the view that matters when a DLQ is flooding |
| Message detail | Headers, `x-death` detail, payload. Payload absent under `sampled`/`none` is stated as a policy outcome, not an error |

### 3.4 The operation dialog — the one screen that must be slow

The dialog carries the dry run, the warning, the rate, the typed environment tag and the button — in that order, on one surface. Three properties, each preventing a specific incident:

- **The dry run is the dialog, not a step before it.** The count and the death reasons are what the operator needs in order to decide, so they are in the same place as the button.
- **The warning names the mechanism**, not the risk. "They will dead-letter again" is actionable; "this operation may be risky" is not.
- **The confirm button carries the count.** A button reading "Replay 1,204" is harder to press by accident than one reading "Confirm".

After execution the dialog becomes a progress view (`/operations/:id`): processed, failed, state, and a halt control.

![03-replay-dialog](images/03-replay-dialog.png)

*Replay. The selection's count and reasons, the body as published and editable, the validity check that keeps the button locked, and the typed environment confirmation.*

![07-peek](images/07-peek.png)

*Peek. Live messages with the `redelivered` flag, and the get-and-requeue caveat stated where the operator acts rather than in a footnote.*

![15-purge](images/15-purge.png)

*Purge. Two typed fields — queue name and environment — and a button that stays disabled until both match.*

### 3.5 Setup wizard (`/setup`)

Five steps. Four block the next; **step 2 does not**. The step strip is a control, not a decoration: completed steps are clickable, and steps 4 and 5 are **locked** until preflight passes, labelled *preflight first*.

| Step | Screen | Blocking condition |
|---|---|---|
| 1 Profile | Provider, endpoint, credential kind, write-only credentials; beside it, a card stating **what enabling capture will create** | — |
| 2 Message store *(optional)* | Engine, host, database, schema, both roles; **Test connection**, and a plain statement of what skipping costs — no counts, no message list, no search, and recovery unaffected (DEC-016) | **None** — it may be skipped, which sets live-only |
| 3 Preflight | One row per check, with a remedy on each failure and `ERR-BRK-001` on a topology failure | A mandatory failure blocks step 4 |
| 4 Select queues | Every discoverable queue and exchange, with backlog and dead-letter routing. **Queues with a DLX are pre-selected**, because they are the ones that can explain a failure | At least one selection |
| 5 Capture | What will be watched, the storage ceiling, and the **backfill offer**: the messages already sitting there, rate-limited and opt-in. With no message store this step says so rather than offering a policy that could not be honoured | — |

**Preflight is re-runnable, and that is the designed path out of step 2.** The most common onboarding outcome is a missing grant; the operator adds it in the broker, re-runs, and the wizard continues. A wizard that cannot recover from its own failure state is a wizard that ends in a support ticket.

Capture is forward-only (C-002), so the backfill offer is the only way to see what is already queued. It is a checkbox, not a default, because it reads the adopter's queue at a rate the operator chooses.

![04-setup-profile](images/04-setup-profile.png)

*Step 1, profile. The provider, the credential kind, and a card stating what enabling capture will create in the broker.*

![04a-setup-storage](images/04a-setup-storage.png)

*Step 2, message store. Optional, and it says what skipping costs: history, not recovery.*

![04b-setup-preflight](images/04b-setup-preflight.png)

*Step 3, preflight. One row per check, and a failing check names the grant that is missing.*

![04c-setup-queues](images/04c-setup-queues.png)

*Step 4, queues. Pre-selected by dead-letter routing, because those are the queues that can explain a failure.*

![04d-setup-capture](images/04d-setup-capture.png)

*Step 5, capture. What will be watched, the ceiling, and the backfill offer as a checkbox rather than a default.*

### 3.6 Publish composer (`/publish`)

The highest-risk screen in the product: it injects a message into a live business flow.

| Guard | Why it is visible rather than implied |
|---|---|
| Body must parse | The button stays disabled and the line under the editor names the error |
| Target restated | Exchange, key and environment beside the body you typed, so the thing you are hitting is legible |
| Idempotency key shown | A double-click cannot produce two messages; a retry returns `AlreadyApplied` |
| Payload hash shown | It is what the audit row records — not the body |
| `prod` typed | The only typed confirmation publish carries, because it is the only one it needs |

The warning closes the loop: *valid JSON is not a valid message*. A well-formed body with wrong semantics passes every check this tool can make, and the consumer rejects it into the dead-letter queue you started from.

![09-publish](images/09-publish.png)

*Publish. The target restated with its environment, the payload hash, and the typed environment tag that unlocks the button.*

### 3.7 Dead-letter list and bulk actions (`/queues/:id`)

The queue detail carries a paginated dead-letter list — time, reason, routing key, correlation id, size — with a checkbox per row and a tri-state select-all in the header cell.

**The actions live in the table header, beside the selection that enables them.** Peek, replay, discard and purge sit on the same line as the count, because an action bar two hundred pixels from the checkboxes is an action bar nobody connects to the checkboxes.

| Rule | Why |
|---|---|
| Nothing acts on "everything" by default | Buttons read `Replay 3` once rows are chosen and are disabled at zero. The label carries the count, so the confirmation cannot be mistaken for a bulk operation |
| Single selection exposes the payload | With one message selected, the replay dialog shows the body and allows it to be edited; with several, it says so plainly and sends each as published |
| Duplicate grouping is a column, not a filter | `×400 same hash` is one producer bug, not four hundred incidents |

**The confirmation shows what it is about to destroy.** Discard and purge open a confirmation that lists the affected messages with their bodies, one click each, before the command is issued — not only in the audit afterwards. This is the same read-only payload dialog the audit uses (§3.11.1), opened in preview: the hash is not shown because it does not exist yet, and the footer says so.

The list is not a preview of a *sample*: every message the confirmation will act on is in it, for the same reason the evidence keeps one row per message (DEC-018). What changes between the confirmation and the audit is only the tense — *will be discarded* becomes *was discarded*.

![08-discard](images/08-discard.png)

*Discard. Every affected body listed before the command, each one openable, and the note that the copy is written before the message is acknowledged.*

![08b-discard-payload](images/08b-discard-payload.png)

*The same body in preview, opened from the confirmation: no hash and no retention date, because neither exists until the operation runs.*

### 3.8 The assistant on the queue screen

What appears here depends on one thing: whether the credential the assistant holds carries `CanExecute` ([agent access](../05-architecture/mcp-integration.md), DEC-010). It is not a preference and not a per-request choice.

**With the grant — the action is already done.** The banner reports it, and offers to show what was touched. It is a notice, not a request.

> 🤖 **Claude replayed 3 messages** · `VIA ASSISTANT`
> *"All three were rejected by the same consumer, which was redeployed at 14:31. They should succeed now."*
> 14:29 as a.reyes@company.com · recorded in the audit — **Show 3**

**Without it — the action is a proposal.** Same reasoning, different ending: a person decides.

> 🤖 **Claude suggested replaying 3 messages** · `PROPOSAL`
> *"…they should succeed now."*
> proposed 2 min ago on behalf of a.reyes@company.com — **Review 3** · **Dismiss**

| Property | Why |
|---|---|
| The reasoning is shown in both | A proposal you cannot evaluate, or an action you cannot explain, is worse than neither |
| The executed form is a notice | No Review, no Dismiss — there is nothing left to decide, and offering a decision that has already happened is a lie about the state |
| The actor is the person, never the model | *14:29 as a.reyes@company.com* — the assistant is the channel, and `via` records that it was |
| `Show 3` selects exactly those messages | The same list the operator would have built by hand, which is what makes the action checkable |
| The proposal form keeps the guard chain | `Review` pre-fills the normal dialog; the agent gets no shortcut, only a form someone else must sign |
| Dismissal is recorded | A pattern of dismissed proposals is how you learn the agent's judgement is off |
| The two forms never appear together | One credential, one of the two — a screen showing both would be asking the operator to work out which one applies |

![10-agent-executed](images/10-agent-executed.png)

*The granted case: an assistant's replay, already done, attributed to the person whose credential carried it — with no approval step, and the reasoning kept with it.*

**The proposal form has no screenshot**, deliberately. The propose path is shown where it leaves a permanent trace: the `proposal` row in the audit (§3.11).

### 3.9 The assistant drawer

A panel over whatever screen is open, because an operator should not leave the queue to ask about it. It is the same agent as the MCP server, over the same tool registry, filtered by the signed-in user's credential ([agent access](../05-architecture/mcp-integration.md) §9).

| Rule | Why |
|---|---|
| The header states the model and what it can see | "headers and reasons only" versus "reads message bodies" is the disclosure, visible on every screen rather than buried in settings |
| The answer is a finding, then evidence, then a proposal | A conclusion without the numbers is a guess with good grammar |
| The proposal card is the same card as the queue screen | One proposal, one review dialog, one audit path — the drawer is a second door, not a second process |
| `Review` closes the drawer and opens the queue | The action happens in the guard chain, never in the chat |
| The empty state offers three questions | An operator mid-incident should not have to think of a good prompt |

**When no model is connected the drawer is still reachable**, and says so: *no model connected*, with **Connect a model** for an Admin and *ask an administrator* for everyone else. The header button is never hidden — hiding it would mean Support never learns the feature exists and the Admin is never prompted.

![13-chat-drawer](images/13-chat-drawer.png)

*The drawer open over the queue list, with three suggested questions rather than an empty box.*

![13b-chat-answer](images/13b-chat-answer.png)

*An answer: the finding, an evidence table, and the completed action card naming who it was done as.*

![12-ai-settings](images/12-ai-settings.png)

*Provider settings. The provider, the model, the payload switch, and a list of every tool with its read or act badge.*

![13c-chat-not-configured](images/13c-chat-not-configured.png)

*No model connected. The button is still there, the drawer explains, and it names who can fix it.*

![13d-chat-not-configured-admin](images/13d-chat-not-configured-admin.png)

*The same state for an Admin, with the fix in it.*

### 3.10 Storage settings (`/storage`)

Storage is asked for at first run (§3.5, step 2) and can be **skipped**, which puts the install in **live-only** mode ([DEC-016](../01-project/decisions-and-open-questions.md)). This screen is where it is added later, tested, or replaced.

| State | What the screen shows |
|---|---|
| **Live-only** | The engine form, and the consequence: *counts begin when this is connected; the period before was never recorded*. It also says what does **not** change — recovery and the audit keep working, because they never lived here |
| **Connected** | Engine, host, database, schema, both roles, schema version, counting-since; **Test connection** and **Replace…** |
| **Replacing** | A warning that captured rows are not migrated and counts restart from the new database |

**No usage figures on this screen, on purpose.** Rows, bytes, the age of the oldest capture row and usage against the ceiling are answered from the database itself — the adopter already monitors the server it lives on, and duplicating that here would invite a reporting feature the product does not want to own. The queries are in [storage prerequisites §7](../11-operations/storage-prerequisites.md).

**The live-only banner appears only where the gap is felt** — the queue list, the report screen and any search — naming the reason and the fix. It must not appear on the audit log or beside a replay button, because neither is degraded: showing it there would teach the operator that their recovery is broken when it is not.

**What live-only costs, and what it does not.** Replay, discard, purge and publish **stay available**. Each owes an audit row (`FR-016`), and the audit lives in Marites' own store, so it is written whether or not the adopter ever connects a message store (DEC-016). What stops is *looking back*: counts over time, the message list, search, and the report screen. An install with no message store can still recover; it cannot remember.

![14-storage](images/14-storage.png)

*Connected. Engine, host, schema, both roles, and what the store holds — and what it deliberately does not.*

![15b-storage-add](images/15b-storage-add.png)

*Live-only. What turns on when a message store is connected, and what was already on.*

![14b-live-only](images/14b-live-only.png)

*Live-only on the queue detail: no history, and recovery still working.*

### 3.11 Audit history and payload evidence

Each audit row expands to show what the operation actually carried. The evidence is a snapshot taken when the operation ran — the bodies sent, or the bodies destroyed — and it lives on **its own retention clock** — 90 days by default, never shorter than the message rows — rather than the audit's (DEC-017). Two shapes, because a single message and a batch answer different questions.

**One message: what was sent, and did it change.** The panel shows the body **in full**, not a preview, and when the operator edited it before replaying, both versions sit side by side with the changed lines marked. The row itself carries an `edited` flag, so an administrator scanning the log sees that a replay was not a plain resend without opening anything.

**A batch: every message, one row each.** The count in the header and the number of rows are the same number — a purge of ten shows ten, and the operator can scroll to any one of them.

**After retention lapses, either shape says so.** The row does not go blank: it names the date the bodies were swept, the retention that applied, and the hash that survives. *Payload no longer retained* on its own reads as a broken panel; the date and the rule turn it into an answer.

| Rule | Why |
|---|---|
| **One row per message, always** | A purge of 1,204 writes 1,204 rows. A sample cannot answer *"was this message in there?"*, and that is the question a dispute asks (DEC-018) |
| **A single body is shown whole** | The administrator's question is *what exactly went back onto the queue*. A preview with an ellipsis answers it only for messages small enough not to matter |
| **Replay stores before and after, and marks the difference** | An edited replay sent something the original does not contain; only the pair proves the change, and the change must be visible without reading both bodies line by line (DEC-011, DEC-018) |
| **The row says `edited` before it is opened** | An edited replay and a plain one look identical in a list; the difference is the thing an administrator is looking for |
| **Peek stores what it showed** | The operator saw those bodies; the record should hold what they saw |
| **Truncation is visible** | Beyond `MARITES_EVIDENCE_MAX_ROWS` the rows stop and the row says `evidence truncated at 10,000 of N` — never a silent partial record |
| Expiry names the date and the rule | "No longer retained" alone invites the reader to think the record is broken. The panel says when it was swept, which retention applied, and that the hash is what survives — so the answer to *where is the payload* is a fact, not a shrug |
| **Evidence outlives the message on purpose, and says so** | Ninety days by default, never shorter than the queue's rows (DEC-017). This is the one place in the product where a payload outlives its message, and the panel states it in words rather than leaving the operator to work out why a discard from August still has a body when the queue only keeps three days |

![11-audit](images/11-audit.png)

*The audit log. Human actions and assistant actions side by side, with `via Claude` on the executed one and a `proposal` row for a credential that may only propose.*

![11b-audit-payload](images/11b-audit-payload.png)

*One replayed message, expanded: the body whole, before and after, with the changed line marked.*

![11c-audit-bulk](images/11c-audit-bulk.png)

*A purge of ten, expanded: ten rows, one per message, because a sample cannot answer the question a dispute asks: was this one in there.*

![11h-audit-expired](images/11h-audit-expired.png)

*Evidence past retention. An operation from 07 August, its 90-day evidence term elapsed on 05 November: the row says when it was swept, why that term applied, and that the hash is what remains.*

### 3.11.1 The body itself

The expanded row is a summary, not the record. Any evidence row opens a dialog holding the complete body, and that dialog is the **same surface for all four operations** — replay, discard, purge and publish — because the question is the same one each time: *what exactly was in that message?*

| Rule | Why |
|---|---|
| **One dialog, four operations** | Replay, discard, purge and publish all answer *what was in it*. Four surfaces would drift into four different amounts of evidence |
| **Line numbers, always** | An administrator comparing a body against an application log, or quoting a line back to a colleague, needs to name the line. "Third line down" is not a citation |
| **Side by side only when the body changed** | A single body in a two-column layout implies a comparison that is not there. One pane, at a narrower width, when there is nothing to compare |
| **The changed line is marked twice** | A tinted row and a `-`/`+` marker: colour alone fails in greyscale, in print, and for a red-green colour-blind reader |
| **Every line, with no scroll-to-find** | The dialog scrolls; the body is not cut to fit. A truncated body would make the evidence argue against itself |
| **The hash and the retention date travel with it** | A body shown without its expiry invites the reader to assume it will still be there tomorrow |

The dialog is read-only. Nothing in it can be edited and re-sent — replay edits in the replay dialog, where the dry run and the typed confirmation live (DEC-011).

![11d-payload-diff](images/11d-payload-diff.png)

*The dialog for an edited replay. Line numbers, both versions, and the change marked twice — a tinted row and a `−`/`+` — so it survives greyscale and print.*

![11e-payload-discard](images/11e-payload-discard.png)

*The same dialog for a discard: one pane, because there is nothing to compare.*

![11f-payload-purge](images/11f-payload-purge.png)

*Opened from one row of a purge, so it shows that message and not the batch.*

![15c-payload-publish](images/15c-payload-publish.png)

*What a publish sent.*

![11g-payload-dark](images/11g-payload-dark.png)

*The same diff in dark. The tint and the markers both survive the palette.*

## 4. Reporting dashboard (`/report`)

Four tiles, one trend and a per-queue table. The executive surface, and the only screen a Viewer can reach — so it carries no operation control, no message body and no setting.

| Rule | Where it is owned |
|---|---|
| Four tiles, never a combined total | [DOC-UX-001 §1](dashboard-and-operations.md) rule 1 |
| Dead-letter count never shown without its rate | rule 2 |
| Reason breakdown always present | rule 3 |
| Granularity in the caption, availability-driven selector | §1, the four granularity rules |
| No message bodies, no operation controls | §1.1 |
| A stale profile is marked, never zeroed | §4 |

The only thing this document adds is **layout**: tiles in a single row, trend beneath, per-queue table last. Everything about what the numbers mean is owned by DOC-UX-001 and is not repeated here.

![05-reporting](images/05-reporting.png)

*Reporting. Four tiles, a trend, and the per-queue table last — with no message bodies and no operation controls anywhere on it.*

![14d-live-only-report](images/14d-live-only-report.png)

*Reporting in live-only. The tiles read `—` rather than `0`, and the trend is replaced by an explanation: a zero would be a measurement, and nothing was measured.*

## 5. Authentication states

Identity is Entra ID (`ADR-008`), so **Marites renders no sign-in form** in the normal case: the SPA redirects, and the identity provider owns the page. That leaves five states this product does own, and they were undesigned until the design exploration forced the question.

| State | What the user sees | Rule |
|---|---|---|
| Signed out | Redirect to Entra; nothing rendered in between | No Marites-branded login form |
| Callback | A quiet loading state, then the role's landing route | Never a flash of the wrong surface |
| `MARITES_AUTH_MODE=none` | No sign-in at all; loopback only | First run must not prompt for a sign-in that does not exist |
| Break-glass | A local form on the Marites origin — the only form Marites owns | Enabled by environment variable, disabled by default, every use audited |
| **Token expires mid-operation** | The dialog keeps its contents and offers re-authentication | **A replay dialog open for ten minutes must not lose its dry run and typed confirmation** |

The last row is the one that matters. A session that expires while an operator is confirming a destructive action must resume, not restart — losing the dry-run result would push them to re-run it quickly, which is exactly the wrong behaviour under pressure.

![00-login](images/00-login.png)

*The sign-in screen in the UI exploration. Marites itself renders no form — the identity provider owns this page (ADR-008) — so this screen exists only to demonstrate the three roles.*

![00-login-dark](images/00-login-dark.png)

*The same, dark.*

![06-dark](images/06-dark.png)

*The queue detail in dark: the same tokens, not a second palette.*

## 6. States that are not a screen

| State | Presentation | Owner |
|---|---|---|
| No broker configured | Redirect to `/setup`, never an empty dashboard | DOC-UX-001 §4 |
| Preflight failing | Console available; capture controls replaced by the failing check and its remedy | DOC-UX-001 §4 |
| Queue has no dead-letter routing | `unavailable` in the DLQ column, banner on the list | DOC-UX-001 §4 |
| Capture stopped | Stale badge with last-seen, beside a rising depth | DOC-UX-001 §4 |
| Window has no data | "No messages in this window", distinct from "capture unhealthy" | DOC-UX-001 §4 |
| Storage capped | "Capped" banner with an explanation that counts continue | DOC-UX-001 §5 |
| Agent proposal pending | A banner above the metrics, with the reasoning and two actions | §3.8 |
| Assistant not configured | The drawer explains, names the owner, and disables its input | §3.9 |
| **Live-only (no message store)** | A banner where the gap is felt — the queue list, the report screen, any search — saying what still works. Recovery and the audit are **not** degraded and carry no warning (DEC-016) | §3.10 |
| Proposal raised, not executed | A `proposal` row in the audit with result `pending`, and a banner on the queue screen when a credential may only propose | [agent access](../05-architecture/mcp-integration.md) §4 |

## 7. Deliberate scope

Four boundaries, each a decision rather than an omission.

| Boundary | The rule | Why |
|---|---|---|
| **Desktop only** | The console is designed for a workstation. Below 900px the rail collapses and the step strip wraps, and nothing further is specified | An incident is diagnosed at a desk with the broker's logs open beside it. A phone layout would be a second design to keep in step for a case nobody has |
| **Errors appear in place** | A failure that blocks the screen is a banner at the top of it; a failure that blocks one control is a line under that control. The message is the error catalog's remedy, not a status code | The operator needs the next action, and the catalog already carries it (`ERR-*` in [the error catalog](../07-data-and-contracts/error-catalog.md)) |
| **The assistant never interrupts** | A proposal that arrives while the queue is open appears as a banner on the next poll. It is never a modal, and it never takes focus | The operator is mid-incident. A dialog that steals the cursor is a dialog that gets dismissed without being read |
| **No loading skeleton** | A screen that has not loaded shows its previous state, dimmed, rather than an empty frame | An empty frame reads as "no messages", which is the one thing this product must never say by accident |

Colour, type and spacing are owned by [visual design](visual-design.md).

## 8. Document control

Revision 0.1 recorded the navigation model, nine routes and the operation dialog. Written November 2026.

*Amended November 2026:* the route map gained `/login`, `/publish` and `/audit` — three screens the contract served and this document had omitted, found while exploring the UI. Added §3.6 (publish composer), §3.7 (dead-letter selection and header actions), §3.8 (agent proposals) and §5 (authentication states, including the expired-token rule). Visual design moved out to [DOC-UX-004](visual-design.md). A further amendment added §3.9 (the assistant drawer), the `/ai` and drawer routes, and the unconfigured state.

*Amended November 2026:* the block mockups were removed and replaced by the UI exploration screenshots, one set per section — a mockup beside a photograph is the same information twice, and the mockup is the copy that drifts. §3.8 was rewritten for both credential states after DEC-010 was revised; §3.3 stopped claiming a separate message-list screen; §6 lost two rows that contradicted DEC-016 and DEC-010; §7 became scope decisions rather than a list of what was unwritten.

*Amended November 2026:* the queue message-list route now names the deferred `QUERY /messages` operation from the API contract.
