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
| Queue metadata | `GET /queues` and `GET /queues/{queueId}` (OP-QUE-001/002) read live from the broker's management API on every call — nothing is cached or stored. Queue ids are derived (UUIDv5, DEC-022) rather than assigned, so an id is stable across reads and identical to the `queue_ref` key M2 will create. Verified against the test broker: ids stable, detail resolves without a prior list, and a profile saved `failing` for missing topology permissions still reads live metadata |
| Live operations | `POST /operations/peek`, `/discard`, `/purge`, `/replay` (OP-OPR-001…004), served at contract revision 0.3.0. Each resolves its queue by the derived id, requires an idempotency key, and answers with the contract's shapes. Discard and purge demand a typed queue-name confirmation before acting (`ERR-OPR-001` on mismatch); purge and replay also demand the environment tag when the target broker is `prod`. Dry runs read the live queue and touch nothing. Peek answers `destructive: true` — RabbitMQ peek is get-and-requeue, measured destructive at M1. Replay's destination is the message's own `x-death` record unless a target is given, and an uneven published/acknowledged count is `202` `Unresolved` (`ERR-OPR-004`), never a plain failure. Vendor exception types stop at the adapter (`BrokerConnectionException` → 503 `ERR-BRK-002`). Verified live against the local stack: discard's dry run previewed without touching depth and its apply removed one; purge emptied the queue; replay moved two messages to an explicit target |
| Preflight | Six contract check ids always returned. Three run against the management API — reachability, vhost visibility, topology-write permission; three report `skipped` with a reason. Verified `passing` against the local RabbitMQ stack, and `failing` with a remedy when a grant was absent |
| Broker creation | Validates before persisting (DEC-021): the broker is checked before anything is written, so an unreachable broker (503 `ERR-BRK-002`) or rejected credentials (422 `ERR-BRK-004`) leave no profile and no stored secret. A topology denial saves the profile as `failing` and returns the initial checks with it |
| Broker API behaviour | Strong ETags (`"3"`), `If-Match` required on update (428 without it, 409 with a stale one, `ERR-CMN-006`), `Idempotency-Key` required on every command (400 without it, replay of the first answer on repeat, `ERR-CMN-005` if the same key carries a different request), and endpoint credentials redacted on read |
| Ports | `ProviderPorts` declares the broker seams in `Application`. No adapter implements them |
| Capture | `CaptureWorker` and `WorkerHost` exist as entrypoints. No consume loop |
| Bookkeeping store | SQLite via EF Core, created by `marites migrate`. All ten bookkeeping tables: `tenant`, `app_user`, `broker_profile`, `queue_ref`, `capture_rule`, `replay_job`, `operation_audit`, `operation_payload`, `operation_intent`, `idempotency_key` — one migration, `0001_bookkeeping_schema`, creating them in dependency order, numbered to match `migration-plan.md` §2.1 |
| Credential protection | `ICredentialProtector` with an AES-256-GCM adapter: a per-row data key wrapped by the KEK (ADR-018), `MARITES_KEK_PREVIOUS` honoured on unwrap for rotation. Base64 keys only; a KMS URI is refused with a message naming the deferral |
| Audit | `operation_audit` is append-only by schema — `BEFORE UPDATE` and `BEFORE DELETE` triggers raise, so a repository cannot forget the rule (FR-016) |
| Tests | **87** — 45 `Marites.InfrastructureTests` (schema, audit path, crypto, AMQP connection, tap, reconnect, backoff, clock skew, replay-target header arithmetic), 11 `Marites.ArchitectureTests` (layer dependencies, host-mode parsing, host startup, plus a fixture that deliberately violates the layering) and 31 `Marites.ApiTests` (broker operations over HTTP, the four live operations over HTTP against stubbed catalog and admin, contract conformance for 2xx bodies, the three DEC-021 creation outcomes, and error statuses read from the catalog rather than hard-coded) |

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

No message has been captured. No provider besides RabbitMQ has an adapter, so
`/capabilities` reports no provider kinds beyond what the test broker answers. Credentials are
encrypted and written, and now used for the live operations — but nothing consumes from a
broker yet, so nothing is observed.

The broker operations and the four live operations are covered over HTTP: statuses, the
revision guard, idempotent replay, redaction, the name conflict, the typed confirmations and
the production environment tag all have a test that fails if they break. No requirement in
`docs/02-business/requirements.md` is met.

## Next

WP-M1-3 hold and promote — `operation_intent` written before the command, promoted on
confirmation, reconciled on restart (ADR-023). The live operations currently execute without
the durable hold; this is the package that makes the crash between command and record
resolvable instead of merely survivable.
