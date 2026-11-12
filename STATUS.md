# Status — react-csharp stack

What this branch has built, as opposed to what the specification describes. The
specification is `docs/`, which is identical on every branch; this file is the only place
that states implementation status for this pairing.

Last reviewed: 2026-11-12 · Documentation baseline: 1.0

## What exists

| Area | State |
|---|---|
| Code | **None, by design.** This branch owns no frontend and no backend source; it carries the rules for running the two together |
| Purpose | The seam: which client talks to which host, how it is configured, what has to be true before the pair is worth demonstrating |

## What runs

OpenAPI contract validation and the docs sync, both inherited from `main`. Nothing
pair-specific, because there is nothing to run.

## What has not happened

Nothing is paired. The `csharp` branch serves no endpoint and the `react` branch renders no
screen, so there is no integration to describe yet — which is the honest reason this file is
short rather than a sign the branch is unfinished.

## Next

When `csharp` serves its first endpoint and `react` renders its first screen, this branch
records how the client is pointed at the host, what the running pair demonstrates, and what
it still cannot do.
