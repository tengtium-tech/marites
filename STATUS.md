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
| Screens | **One**: the setup wizard. Saves a broker profile, runs preflight, and disables *Enable capture* until preflight passes — saving stays allowed so a mistyped credential can be corrected |

## What runs

`React client validation` on every push and pull request: `npm ci`, generate the API client,
typecheck it, then verify the deferred `QUERY` output. `npm run build` produces a bundle
locally; it is not yet part of the workflow. Green as of this commit — which is the
one thing on either stack that is both automated and passing.

## What has not happened

The wizard has not been driven in a browser against a live API. `npm run build` passes and the shell renders, but no one has clicked Save and watched the preflight results come back — that is the next thing to do, and the acceptance criterion depends on it. No
accessibility check has run, so NFR-008 is unmet rather than partially met.

## Next

The first screen, and a decision on issue #37 — how the client represents an HTTP `QUERY`
request in TypeScript, which is the open question this branch's compatibility test exists to
keep visible.
