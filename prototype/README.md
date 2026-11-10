# Marites UI prototype

**Exploration only. Not authoritative.** The specification set in [`../docs`](../docs/README.md) is the source of truth; where this prototype disagrees with it, the documents win.

**The mark is an ear.** A *Marites* is the neighbour who always has the latest — this one listens to traffic instead of gossip. The glyph is a 24px stroke ear on an indigo tile, with the same artwork as `public/favicon.svg`.

A clickable UI built with the stack the specification names — React 18, TypeScript, Vite, Tailwind, and **shadcn/ui** components (Radix primitives copied into `src/components/ui`, per the shadcn model). No backend, no data layer, no tests: every number on screen is hard-coded.

## Run it

Run these from **this folder** (`prototype/`), not from the repository root — `package.json` lives here.

```bash
cd prototype     # from the repository root; or just cd into this folder
npm install      # once, or after package.json changes
npm run dev      # http://127.0.0.1:5173
```

`npm install` creates `node_modules/` (about 120 MB, gitignored). If port 5173 is already taken, Vite prints the port it used instead — read the line it prints rather than assuming 5173.

## Screens

| | |
|---|---|
| ![Sign in](screens/00-login.png) | ![Sign in, dark](screens/00-login-dark.png) |
| **Sign in** — three demo accounts; each lands on the surface its role can use | **Sign in, dark** — the same tokens |
| ![Queue list](screens/01-queues.png) | ![Queue detail](screens/02-queue-detail.png) |
| **Queues** — `captureState` as a word, dead letters with their rate, `unavailable` instead of `0` | **Queue detail** — the actions sit in the table header, beside the selection that enables them |
| ![Replay dialog](screens/03-replay-dialog.png) | ![Peek](screens/07-peek.png) |
| **Replay** — the selected message's body, editable, with a validity check before the button unlocks | **Peek** — live messages, with the get-and-requeue caveat stated plainly |
| ![Discard](screens/08-discard.png) | ![Setup, step 1](screens/04-setup-profile.png) |
| **Discard** — every body is listed before the command, and the copy is written first | **Setup, step 1** — the profile, and what enabling capture will create |
| ![Setup, step 2](screens/04b-setup-preflight.png) | ![Setup, step 3](screens/04c-setup-queues.png) |
| **Setup, step 2** — a failing check blocks the wizard, and names the grant | **Setup, step 3** — queues pre-selected by dead-letter routing |
| ![Setup, step 4](screens/04d-setup-capture.png) | |
| **Setup, step 4** — what will be captured, and the backfill offer | |
| ![Assistant replay](screens/10-agent-executed.png) | ![Purge](screens/15-purge.png) |
| **Assistant replay** — done, attributed to the person whose credential carried it | **Purge** — two typed fields, and the button stays disabled until both match |
| ![Audit](screens/11-audit.png) | ![Audit evidence](screens/11b-audit-payload.png) |
| **Audit** — agent proposals are their own rows, beside the human action that followed | **One message** — the body in full, before and replayed, with the edited line marked |
| ![Audit evidence, batch](screens/11c-audit-bulk.png) | ![Publish](screens/09-publish.png) |
| **A batch** — a purge of ten shows ten bodies, one row per message, never a sample | **Publish** — composer, target summary, payload hash, typed `prod` |
| ![Payload dialog, edited replay](screens/11d-payload-diff.png) | ![Payload dialog, discard](screens/11e-payload-discard.png) |
| **The body, edited** — one dialog for all four operations, line numbers, the change marked `-` and `+` | **The body, destroyed** — what a discard removed, whole, one pane |
| ![Payload dialog, purge](screens/11f-payload-purge.png) | ![Payload dialog, dark](screens/11g-payload-dark.png) |
| **The body, purged** — clicking a row opens that message, not the batch | **The same dialog, dark** — the tint and the markers both survive |
| ![Storage](screens/14-storage.png) | ![Live only](screens/14b-live-only.png) |
| **Storage** — one database per install; replace warns that counts restart | **Live only** — no storage, so no history, no audit, no recovery |
| ![AI settings](screens/12-ai-settings.png) | ![Chat drawer](screens/13-chat-drawer.png) |
| **AI settings** — bring your own model; local Ollama keeps everything in your network | **Chat drawer** — three suggested questions; it reads and proposes |
| ![Chat answer](screens/13b-chat-answer.png) | ![Reporting](screens/05-reporting.png) |
| **An answer** — the finding, the evidence, and a proposal you review | **Reporting** — four tiles, a trend, per-queue breakdown |
| ![Not configured](screens/13c-chat-not-configured.png) | ![Not configured, admin](screens/13d-chat-not-configured-admin.png) |
| **No model connected** — the button is always there; the drawer explains, and names the owner | **For an admin** — the same state with the fix in it |
| ![Dark theme](screens/06-dark.png) | |
| **Dark theme** — the same tokens, not a second palette | |

## Demo accounts

Sign in with any of these; the password can be anything. Each one lands on a different surface, because the role comes from the account rather than a toggle.

| Email | Role | Lands on | Sees |
|---|---|---|---|
| `viewer@company.com` | Viewer | Reporting | Volume, trend, dead-letter rate. No console at all |
| `support@company.com` | Support | Queues | The recovery loop: peek, discard, replay, publish. No purge, no Setup |
| `admin@company.com` | Admin | Queues | Everything, plus purge, broker profiles and capture policy |

Signing out returns to this screen. The session is kept in `sessionStorage`, so a refresh keeps you in — clear it or sign out to switch.

**This screen is a prototype device, not the product.** The real build redirects to Entra ID and Marites never renders a sign-in form (`ADR-008`). It exists so role-based entry can be demonstrated, and as a sketch of the break-glass form, which the specification leaves undesigned.

## What it demonstrates

| Screen | Route | Shows |
|---|---|---|
| Sign in | `/` | Three demo accounts, an email form, and the Entra caveat |
| Queues | `#queues` | Queue list with depth, rate, dead letters and `captureState`; the banner for queues that cannot report failures |
| Queue detail | `#queue` | Metric strip, the action bar, capture policy and failure-reason breakdown |
| Replay dialog | `#replay` | The selection's count and reasons, an **editable JSON payload** when one message is selected, the warning that replaying will dead-letter again, and the typed environment confirmation |
| Peek | `#peek` | Live messages, the `redelivered` flag, and the get-and-requeue caveat |
| Discard | `#discard` | Typed queue-name confirmation, with every affected body listed and openable |
| Discard, one body | `#discard-payload` | The confirmation with one body open in preview: no hash yet, no retention date yet |
| Purge | `#purge` | Admin only: typed queue name **and** environment tag |
| Publish | `#publish` | Compose a message: exchange, routing key, JSON body, typed environment tag |
| Audit | `#audit` | Operations log: human actions and agent proposals, side by side |
| Audit evidence, one message | `#audit-payload` | A replay of one message, expanded: the body whole, before and replayed, the edited line marked, and `edited` on the row itself |
| Audit evidence, batch | `#audit-bulk` | A purge of ten, expanded: ten bodies, one row per message |
| Payload dialog, edited replay | `#payload-diff` | The body dialog, open on an edited replay: line numbers, before and replayed |
| Payload dialog, discard | `#payload-discard` | The same dialog for a discard — one pane, the body that was destroyed |
| Payload dialog, purge | `#payload-purge` | The same dialog opened from one row of a purge |
| Payload dialog, publish | `#payload-publish` | The same dialog for what a publish sent |
| Storage | `#storage` | The database: connected state, add it later, or replace it (Admin) |
| AI settings | `#ai` | Bring your own model: provider, base URL, key, payload toggle (Admin) |
| Chat drawer | `#chat` | The assistant as a side panel, over whatever screen you are on |
| Reporting | `#report` | Four tiles, a trend, and the per-queue table |
| Setup | `#setup` | The four-step wizard; `?step=3` opens it at a given step |

Add `?dark=1` to any route for the dark palette.

## The rules it is built to

These come from the documents and are the reason the UI looks the way it does:

- **Status is a word plus a tinted chip, never colour alone** — `live`, `stale`, `capped`, `disabled`, `unavailable` (`VOCABULARY` §15, `DOC-UX-002` §2.2).
- **A dead-letter count is never shown without its rate**, and `unavailable` is never rendered as `0`.
- **Controls are omitted, not disabled**, when a role or provider cannot use them. Sign in as `viewer@company.com` and the console disappears entirely; sign in as `support@company.com` and both purge and Setup are absent rather than greyed.
- **Four tiles, no combined total** — `published` and `dead_lettered` overlap by construction.
- **The dialog is the dry run**: the count, the reasons and the warning sit in the same place as the button.
- **The payload is editable, and the dialog says what that costs.** With one message selected you get the body as published, a validity check that blocks replay while the JSON does not parse, and — the moment you change a byte — a note that the audit row records the hash of what you send, so the replay is no longer provably the message that failed.
- **History keeps what was sent, on the message's clock.** Every audit row expands to the bodies it carried — what a replay actually sent, what a discard or purge destroyed. The evidence is a snapshot in its own table, deleted on the same schedule as the messages, so the audit stays permanent and small. After it lapses the row says *payload no longer retained* and keeps the hash.
- **An edited replay looks different from a plain one.** One message opens as the body in full — not a preview — with the original and the replayed version side by side and the changed lines picked out; the row itself carries `edited`, so an administrator sees that a replay was not a plain resend without opening anything. A batch lists every message it touched: purge ten, see ten. The question an administrator is asked is *what exactly went back onto the queue*, and neither an ellipsis nor a sample answers it.
- **The body gets a surface of its own.** Clicking any evidence row opens the same read-only dialog for replay, discard, purge and publish: line numbers in a gutter, one pane when nothing changed and two when something did, the difference marked by both a tint and a `-`/`+`, and the hash and expiry in the footer. Four operations, one view — otherwise they drift into four different amounts of evidence.
- **Storage can be added later, and the cost is stated at the moment you add it.** Skip it at first run and the install is *live-only*: queue metadata and peek work, nothing else does. Connecting a database turns on history, audit and recovery — and the screen says **counts begin now**, because the period before was never recorded and cannot be backfilled.
- **No storage means no recovery.** Replay, discard, purge and publish are disabled while live-only, because each one owes an audit row (`FR-016`) and there is nowhere to write it. That is what makes adding storage later mean "recovery", not just "history".
- **The assistant is the same agent, hosted differently.** The MCP server lets Claude Desktop ask Marites; the drawer lets Marites ask the model. Both use one tool registry — five read tools, four propose tools — so the two surfaces cannot drift. Neither can execute.
- **The button is always visible; the drawer explains why it cannot answer.** Hiding *Ask Marites* until an AI is configured would mean Support never learns the feature exists and the admin is never prompted to enable it. So it shows, and the drawer says *no model connected* — with **Connect a model** for an admin and *ask an administrator* for everyone else.
- **Bring your own model, and local is first-class.** Azure OpenAI, OpenAI, Anthropic, or Ollama on your own network. The local option is the default in the settings screen because for an air-gapped or regulated install it is the only acceptable answer — and it is the one that makes "self-hosted" true.
- **An agent may look and propose; a human approves.** Claude can read every queue, message and metric, and can propose a replay with its reasoning. It cannot send anything. Approving records the *person* as the actor and leaves the proposal in the audit log beside the action — so a replay at 3am is attributable to a human, and the suggestion that prompted it is still visible.
- **Publish is the highest-risk screen, so it is the slowest.** The body must parse before the button unlocks, the target is restated with its environment, the payload hash is shown, and a note says the audit row records the hash rather than the body. A warning states the obvious consequence: valid JSON is not a valid message, and a rejected one lands in the dead-letter queue you started from.
- **Every action works on a selection, from the table header.** Peek, replay, discard and purge sit in the header of the dead-letter table — the same place the checkboxes are — and read `Replay 3` once rows are chosen. Nothing acts on "everything" by default. Purge is the exception, and it is the one with two typed confirmations.

## What it is not

Not a design system. The tokens in `src/index.css` follow [the visual design document](../docs/experience/visual-design.md), which owns the palette, the type scale and the computed contrast ratios; the prototype implements them rather than defining them. No accessibility audit has been run beyond the contrast pairs, which were computed rather than measured in a browser.
