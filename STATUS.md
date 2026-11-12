# Status — csharp stack

What this branch has built, as opposed to what the specification describes. The
specification is `docs/`, which is identical on every branch; this file is the only place
that states implementation status for this stack.

Last reviewed: 2026-11-12 · Documentation baseline: 1.0

## What exists

| Area | State |
|---|---|
| Solution | `Marites.slnx`, seven projects under `src/`, two test projects under `tests/` |
| Layers | `Domain`, `Application`, `Infrastructure`, `Api` — the four layers of ADR-025, with `Contracts` and `Worker` alongside |
| API | **One served operation**: `GET /api/v1/capabilities` (OP-CAP-001). Both feature lists are empty on purpose — a feature is listed when its operations are served, not when they are planned |
| Ports | `ProviderPorts` declares the broker seams in `Application`. No adapter implements them |
| Capture | `CaptureWorker` and `WorkerHost` exist as entrypoints. No consume loop |
| Bookkeeping store | SQLite via EF Core, created by `marites migrate`. Four tables: `tenant`, `app_user`, `broker_profile`, `operation_audit` — migrations `0001`, `0002`, `0003`, `0007`, numbered to match `migration-plan.md` §2.1 |
| Credential protection | `ICredentialProtector` with an AES-256-GCM adapter: a per-row data key wrapped by the KEK (ADR-018), `MARITES_KEK_PREVIOUS` honoured on unwrap for rotation. Base64 keys only; a KMS URI is refused with a message naming the deferral |
| Audit | `operation_audit` is append-only by schema — `BEFORE UPDATE` and `BEFORE DELETE` triggers raise, so a repository cannot forget the rule (FR-016) |
| Tests | **26** — 11 `Marites.ArchitectureTests` (layer dependencies, host-mode parsing, host startup, plus a fixture that deliberately violates the layering) and 15 `Marites.InfrastructureTests` (schema, round-trip, audit triggers, migrator idempotency, nine crypto cases) |

## What runs

`marites migrate` applies the schema and seeds the single implicit tenant, then exits —
the separate step DEC-014 requires. Verified against a fresh file: four tables, two
triggers, one tenant, and a second run that reports the tenant already exists.

`.NET build and test` runs on every pull request and push to this branch: restore, build
with `-warnaserror`, then the 26 tests. That is the gate which makes `LayerDependencyTests`
real — the only automated guard on ADR-025's dependency rule, and previously the thing
nothing ran. Warnings are errors on purpose: a known-vulnerable dependency should not merge
quietly, and the answer to a new advisory is a version bump rather than a suppression.

`global.json` pins the SDK to the 10.0 feature band with `rollForward: latestFeature`, so a
runner image update cannot change the toolchain underneath a green build.

OpenAPI contract validation and the docs sync, both inherited from `main`.

## What has not happened

No broker has been contacted. No message has been captured. No adapter exists, so no
provider is registered and `/capabilities` reports none. No credential has been written to
the store — the protector is tested but nothing calls it yet, because broker CRUD is the
next package. No requirement in `docs/02-business/requirements.md` is met.

## Next

Broker CRUD (#45). It is the first caller of the credential protector, which is currently
tested but unused, and the first package whose operations the frontend can gate on.
