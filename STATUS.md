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
| Screens | None. No application shell, no components, no routing |

## What runs

`React client validation` on every push and pull request: `npm ci`, generate the API client,
typecheck it, then verify the deferred `QUERY` output. Green as of this commit — which is the
one thing on either stack that is both automated and passing.

## What has not happened

No screen renders. No backend has been called, because there is no endpoint to call yet. No
accessibility check has run, so NFR-008 is unmet rather than partially met.

## Next

The first screen, and a decision on issue #37 — how the client represents an HTTP `QUERY`
request in TypeScript, which is the open question this branch's compatibility test exists to
keep visible.
