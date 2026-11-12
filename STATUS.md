# Status — csharp stack

What this branch has built, as opposed to what the specification describes. The
specification is `docs/`, which is identical on every branch; this file is the only place
that states implementation status for this stack.

Last reviewed: 2026-11-12 · Documentation baseline: 1.0

## What exists

| Area | State |
|---|---|
| Solution | `Marites.slnx`, seven projects under `src/` |
| Layers | `Domain`, `Application`, `Infrastructure`, `Api` — the four layers of ADR-025, with `Contracts` and `Worker` alongside |
| API | `ApiHost` builds an ASP.NET Core application with a 30-second shutdown timeout. No endpoints |
| Ports | `ProviderPorts` declares the broker seams in `Application`. No adapter implements them |
| Capture | `CaptureWorker` and `WorkerHost` exist as entrypoints. No consume loop |
| Storage | `SystemClock` only. No persistence, no schema |
| Tests | `Marites.ArchitectureTests` — layer dependencies, host-mode parsing, host startup — plus `Marites.ArchitectureViolationFixture`, a project that deliberately violates the layering so the rule can be shown to fail |

## What runs

OpenAPI contract validation and the docs sync, both inherited from `main`.

**No build or test job.** `dotnet build` and `dotnet test` have never run in CI, so
`LayerDependencyTests` — the only automated guard on ADR-025's dependency rule — is present
and unenforced. That is the largest gap on this branch.

## What has not happened

No broker has been contacted. No message has been captured. No database exists. No endpoint
has been served, so no requirement in `docs/02-business/requirements.md` is met.

## Next

A `dotnet` workflow that restores, builds and runs the tests on every push to this branch.
Without it, the architecture test is documentation with a `.cs` extension.
