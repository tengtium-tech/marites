# Status — react stack

What this branch has built, as opposed to what the specification describes. The
specification is `docs/`, which is identical on every branch; this file is the only place
that states implementation status for this stack.

Last reviewed: 2026-11-12 · Documentation baseline: 1.0

## What exists

| Area | State |
|---|---|
| Client | `apps/web` generates types and a request client from `openapi.yaml` with orval |
| Contract compatibility | `tests/contract-compat` type-checks the deferred `QUERY /messages` operation and verifies the generated output, because most generators do not support HTTP `QUERY` |
| Shell | Vite + React + router, one route (`/setup`), dev proxy from `/api` to the API host so the browser stays on one origin |
| Shell | Header with brand and two destinations — Setup and Queues — and nothing else. Routes the contract does not serve yet are absent rather than present-and-404 |
| Screens | **Two**: the setup wizard and the queue list. The wizard follows the wireframes' two-column layout with a guidance card; saving returns the initial checks, which render as rows with remedies, and **Continue to queues** opens live metadata for the saved broker after either 201 outcome — including a profile saved `failing` for missing topology permissions. Design tokens come from DOC-UX-004. Saving posts to `POST /brokers`, which validates the broker before persisting (DEC-021) and returns the initial checks with the created profile — the wizard renders those rather than calling preflight itself. A refused creation keeps the form editable and shows the remedy; a recheck is an explicit action, gated on the backend's reported `features` and showing the provider's `can*` flags read from `/brokers/{id}/capabilities` — the form is not offered unless `brokers` is served, and an unreachable backend shows a message rather than a control that cannot work, with a structural accessibility pass — native controls throughout, `aria-live` on the preflight results, focus moved to a failed save, `aria-busy` while saving, and hints associated by `aria-describedby` rather than left in placeholders. Saves a broker profile, runs preflight, and keeps capture unavailable until preflight passes — saving stays allowed so a mistyped credential can be corrected |

## What runs

`React client validation` on every push and pull request: `npm ci`, generate the API client,
typecheck it, then verify the deferred `QUERY` output. `npm run build` produces a bundle
locally; it is not yet part of the workflow. Green as of this commit — which is the
one thing on either stack that is both automated and passing.

## What has not happened

No accessibility audit has run, so NFR-008 remains unverified.

## Local Google identity verification

On 2026-11-14, the local preview paired this React work with the C# implementation
from PR #100. A valid Google sign-in without a mapping showed the role refusal.
After an explicit Admin grant, the operator signed in, saved a Docker RabbitMQ
profile with passing preflight, and opened its live queue list. Opening `orders`
loaded live metadata, and **Back to queues** preserved the broker filter.
Support and Viewer browser sign-in and the full TC-031 matrix remain unverified.

## Next

The first screen, and a decision on issue #37 — how the client represents an HTTP `QUERY`
request in TypeScript, which is the open question this branch's compatibility test exists to
keep visible.
