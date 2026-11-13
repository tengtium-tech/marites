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
| API | **Seven served operations**: `GET /api/v1/capabilities` (OP-CAP-001), broker CRUD — `listBrokers`, `createBroker`, `getBroker`, `updateBroker`, `deleteBroker` (OP-BRK-001…005) and `POST /brokers/{id}/preflight` (OP-BRK-006). `/capabilities` now reports `brokers`, because it is true; `providerKinds` stays empty until an adapter exists |
| Operations endpoints | `/health` (liveness) and `/ready` (readiness, checked against the bookkeeping store) on a separate admin listener, `MARITES_ADMIN_ADDR`, default `127.0.0.1:9090`. Both answer 404 on the user port, so health is never exposed where users reach the API |
| Preflight | Six contract check ids always returned. Three run against the management API — reachability, vhost visibility, topology-write permission; three report `skipped` with a reason. Verified `passing` against the local RabbitMQ stack, and `failing` with a remedy when a grant was absent |
| Broker creation | Validates before persisting (DEC-021): the broker is checked before anything is written, so an unreachable broker (503 `ERR-BRK-002`) or rejected credentials (422 `ERR-BRK-004`) leave no profile and no stored secret. A topology denial saves the profile as `failing` and returns the initial checks with it |
| Broker API behaviour | Strong ETags (`"3"`), `If-Match` required on update (428 without it, 409 with a stale one, `ERR-CMN-006`), `Idempotency-Key` required on every command (400 without it, replay of the first answer on repeat, `ERR-CMN-005` if the same key carries a different request), and endpoint credentials redacted on read |
| Ports | `ProviderPorts` declares the broker seams in `Application`. No adapter implements them |
| Capture | `CaptureWorker` and `WorkerHost` exist as entrypoints. No consume loop |
| Bookkeeping store | SQLite via EF Core, created by `marites migrate`. All ten bookkeeping tables: `tenant`, `app_user`, `broker_profile`, `queue_ref`, `capture_rule`, `replay_job`, `operation_audit`, `operation_payload`, `operation_intent`, `idempotency_key` — one migration, `0001_bookkeeping_schema`, creating them in dependency order, numbered to match `migration-plan.md` §2.1 |
| Credential protection | `ICredentialProtector` with an AES-256-GCM adapter: a per-row data key wrapped by the KEK (ADR-018), `MARITES_KEK_PREVIOUS` honoured on unwrap for rotation. Base64 keys only; a KMS URI is refused with a message naming the deferral |
| Audit | `operation_audit` is append-only by schema — `BEFORE UPDATE` and `BEFORE DELETE` triggers raise, so a repository cannot forget the rule (FR-016) |
| Tests | **36** — 11 `Marites.ArchitectureTests` (layer dependencies, host-mode parsing, host startup, plus a fixture that deliberately violates the layering), 16 `Marites.InfrastructureTests` (schema, round-trip, audit triggers, migrator idempotency, idempotency-key uniqueness, nine crypto cases) and 9 `Marites.ApiTests` (the broker operations over HTTP against a real Kestrel host, plus contract conformance: every 2xx body validated against `openapi.yaml` at run time) |

## What runs

`marites migrate` applies the schema and seeds the single implicit tenant, then exits —
the separate step DEC-014 requires. Verified against a fresh file: four tables, two
triggers, one tenant, and a second run that reports the tenant already exists.

`.NET build and test` runs on every pull request and push to this branch: restore, build
with `-warnaserror`, then the 36 tests. That is the gate which makes `LayerDependencyTests`
real — the only automated guard on ADR-025's dependency rule, and previously the thing
nothing ran. Warnings are errors on purpose: a known-vulnerable dependency should not merge
quietly, and the answer to a new advisory is a version bump rather than a suppression.

`global.json` pins the SDK to the 10.0 feature band with `rollForward: latestFeature`, so a
runner image update cannot change the toolchain underneath a green build.

OpenAPI contract validation and the docs sync, both inherited from `main`.

## What has not happened

No broker has been contacted. No message has been captured. No adapter exists, so no
provider is registered and `/capabilities` reports no provider kinds. Credentials are
encrypted and written, but never used: nothing authenticates against a broker yet.

The broker operations are now covered over HTTP: statuses, the revision guard, idempotent
replay, redaction and the name conflict all have a test that fails if they break. No
requirement in `docs/02-business/requirements.md` is met.

## Next

WP-M0-6 preflight — the first code that talks to a broker, and the point at which the
encrypted credentials are finally used for something.
