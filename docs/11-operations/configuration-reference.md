---
document_id: DOC-OPS-007
title: Configuration Reference
product: Marites
status: Approved — Product owner, 2026-11-11
version: 0.1
date: 2026-11
---

# Configuration reference

Every setting an operator can set, in one table. Five of these names appeared across the set without ever being defined together — `MARITES_AUTH_MODE`, `MARITES_URL`, `MARITES_STORAGE_CEILING_BYTES`, `MARITES_EVIDENCE_MAX_ROWS`, `MARITES_PARTITION_INTERVAL` — which meant an operator had to reconstruct the deployment from four documents and a guess. The rest are named here for the first time, because a reference that lists only what happened to be mentioned elsewhere is not a reference.

## 1. What belongs in the environment, and what does not

| Belongs in the environment | Belongs in the database |
|---|---|
| Deployment shape: how to listen, where the stores are, how identity works | Broker credentials, encrypted per row |
| Knobs with a defensible default that a platform team may override | The AI provider key |
| Values a container orchestrator can inject without a restart of the config | Capture policy, retention, ceilings per queue |

**Secrets are never environment variables.** A credential in `docker-compose.yml` is a credential in a repository. Broker credentials and the provider key are written through the API, encrypted per row, and returned by no endpoint (NFR-003). The one exception is the *message-store* connection string, which is a deployment input and is stored encrypted on first run rather than read on every start.

**The key-encryption key is the second exception, and it has to be.** A key stored in the database it protects is not a key. `MARITES_KEK` comes from the environment or a KMS reference, is required, and is the only secret the product cannot write for you:

| Variable | Type | Default | Required | Effect |
|---|---|---|---|---|
| `MARITES_KEK` | base64, 32 bytes, or a KMS URI | — | **Yes**, from first start | Wraps the per-row data keys that encrypt broker credentials (ADR-018) |
| `MARITES_KEK_PREVIOUS` | same | — | Only during rotation | Tried when the current key does not unwrap a row. Removed after `rotate-kek` reports zero rows remaining ([runbook §8](runbook.md)) |

## 2. The variables

| Variable | Type | Default | Required | What it does |
|---|---|---|---|---|
| `MARITES_URL` | URL | — | **Yes**, in any non-local deployment | The public base URL. Used for the OAuth redirect, for links in proposals, for the `audience` check on tokens, and to decide whether the session cookie is `Secure`. A wrong value produces a login loop, not an error |
| `MARITES_LISTEN` | `host:port` | `:8080` | No | Where the HTTP API listens. The only port a user needs |
| `MARITES_ADMIN_ADDR` | `host:port` | `:9090` | No | Health and metrics. Must **not** be published to the user network |
| `MARITES_AUTH_MODE` | `entra` \| `google` \| `none` | `entra` | No | Selects the identity mode. `google` validates a Google ID token and issues a Marites session; `entra` keeps per-request bearer tokens; `none` disables identity and is accepted **only** when `MARITES_LISTEN` is loopback — a guard, not a suggestion |
| `MARITES_ENTRA_TENANT_ID` | GUID | — | When `AUTH_MODE=entra` | The directory that issues tokens |
| `MARITES_ENTRA_AUDIENCE` | string | — | When `AUTH_MODE=entra` | The audience a token must carry; a token for another app is refused |
| `MARITES_GOOGLE_CLIENT_ID` | string | — | When `AUTH_MODE=google` | The OAuth client ID from Google Cloud Console. **Public configuration, not a secret** — it identifies the app, and the browser needs it too. The client secret is never used: the popup flow is implicit/ID-token only |
| `MARITES_SESSION_HOURS` | integer | `8` | No | Absolute lifetime of a `google`-mode session, 1–72. Shorter is safer; long enough to finish an incident review is the point |
| `MARITES_BREAK_GLASS_ENABLED` | boolean | `false` | No | Enables the local break-glass admin. Every use is audited; leaving it off is the default in every auth mode |
| `MARITES_BOOKKEEPING_PATH` | path | `./data/marites.db` | No | Marites' own store — config, audit, evidence, operations. Created if absent (DEC-012) |
| `MARITES_MESSAGE_STORE` | connection string | — | No | The adopter's message store. **Absent means live-only**: no counts, no message list, no search, and full recovery (DEC-016) |
| `MARITES_MIGRATION_CONNECTION` | connection string | — | Only for `marites migrate` | The DDL role, used briefly and never by the running service (DEC-014) |
| `MARITES_STORAGE_CEILING_BYTES` | integer | `53687091200` (50 GiB) | No | The ceiling `ERR-QUE-003` enforces and the sweeper pauses at |
| `MARITES_PARTITION_INTERVAL` | `daily` \| `hourly` | `daily` | No | Partition width for `message_capture`. Changing it affects new partitions only |
| `MARITES_EVIDENCE_MAX_ROWS` | integer | `10000` | No | The per-operation evidence bound. Beyond it the audit row records `payloadTruncated` (DEC-018) |
| `MARITES_EVIDENCE_RETENTION_DAYS` | integer | `90` | No | How long payload evidence is kept, ceiling 365. `retained_until` is `max(this, the queue's row retention)`, so evidence never dies before the messages it describes (DEC-017) |
| `MARITES_REDELIVERY_WINDOW_SECONDS` | integer | `3600` | No | How long `capture_dedupe` holds an identity. Shorter than the broker's redelivery window under-counts (ADR-019) |
| `MARITES_CAPTURE_BATCH_ROWS` | integer | `500` | No | Rows per `COPY`. Lowering it costs throughput; raising it costs memory |
| `MARITES_PREFETCH_COUNT` | integer | `1000` | No | Maximum in-flight messages per capture consumer; adapters map this to the provider's flow-control setting. Must exceed `MARITES_CAPTURE_BATCH_ROWS` for batch-triggered flushes; tune with the M2 throughput test |
| `MARITES_LOG_LEVEL` | `debug` \| `info` \| `warn` \| `error` | `info` | No | `debug` logs message **headers**, never bodies |
| `MARITES_POLL_INTERVAL_SECONDS` | integer | `10` | No | How often the broker is polled for depth, rates and consumers. It also sets the definition of *stale*: three intervals with no rollup write |
| `MARITES_OPERATIONS_PER_MINUTE` | integer | `30` | No | The per-tenant token bucket for mutating operations (THR-015). Exceeding it returns `429` with `Retry-After` |
| `MARITES_OPERATIONS_CONCURRENT` | integer | `5` | No | Simultaneous mutating operations per tenant. A sixth waits rather than failing |
| `MARITES_CONFIRMATION_TTL_SECONDS` | integer | `60` | No | How long a confirmation token stays valid. Longer widens the window in which a queue can change between preview and execute |
| `MARITES_BACKFILL_PER_SECOND` | integer | `20` | No | Ceiling on a backfill's read rate, so a live consumer is not starved. Backfill is opt-in and bounded per run (capture lifecycle §4) |

**Two of these change behaviour rather than tuning it.** `MARITES_MESSAGE_STORE` decides whether the install can look back; `MARITES_AUTH_MODE` decides who may look at all — and `none` is the one value that removes identity entirely, which is why it is loopback-only. Everything else is a dial.

**Who may sign in is not an environment variable.** The role mapping lives in `app_user`, keyed by the verified `(issuer, subject)`, and is written with `marites identity grant` — an operator action that leaves a row, not a JSON blob in a compose file. A Google account with no mapping is refused, and no environment variable can change that into a default role.

## 3. Values that are not variables

Some things look configurable and deliberately are not:

| Not configurable | Why |
|---|---|
| Schema name | Fixed at `marites` in the message store; a configurable schema breaks every grant in [storage prerequisites](storage-prerequisites.md) |
| Migration order | Applied from the embedded set, in order, exactly once (DEC-014) |
| Audit retention | Permanent. There is no setting that shortens it, and that is the point |
| Tenant model | One implicit tenant in single-broker mode, row-level policy in multi-tenant (ADR-014) |
| Role mapping | `app_user` rows keyed by `(issuer, subject)`, written by `marites identity grant`. A domain-suffix or email-pattern rule would admit everyone in a workspace and cannot be audited per person |
| Google client secret | Never configured, because the popup ID-token flow does not use one. If a value is ever needed, it belongs in a secret store, not here |

## 4. Startup validation

The service validates its configuration **before** it binds a port, and refuses to start rather than running half-configured:

| Condition | Result |
|---|---|
| `AUTH_MODE=none` with a non-loopback `MARITES_LISTEN` | Refuses to start; the message names the address |
| `AUTH_MODE=entra` with a missing tenant or audience | Refuses to start |
| `MARITES_MESSAGE_STORE` present but unreachable | Starts, **live-only**, and says so on every degraded screen — a store that is down must not stop recovery |
| `MARITES_MESSAGE_STORE` present with a schema version older than the binary | Refuses to start; `ERR-MIG-001`, naming the installed and required versions |
| `MARITES_BOOKKEEPING_PATH` unwritable | Refuses to start. There is no audit without it, and no recovery without an audit |
| `MARITES_EVIDENCE_RETENTION_DAYS` above 365, or below 1 | Refuses to start. A retention longer than the ceiling is a payload kept for a term the product never promised, and a value of zero would delete evidence as it is written |

**The asymmetry is deliberate.** A missing message store degrades; a missing bookkeeping store is fatal. One is history, the other is the record that the product exists to keep.

## 5. Changing a value

Most are read once at start; changing them requires a restart, and the service logs the values it started with so a drift between the compose file and the running process is visible.

Two are read per request — `MARITES_URL` and `MARITES_LOG_LEVEL` — because they are safe to change and annoying to restart for.

## 6. Document control

Revision 0.1, November 2026. The five previously-named variables are recorded as they were used; the rest are named here for the first time and are therefore **specification, not description** — no binary reads them yet. Where a default is a judgement rather than a constraint (the 50 GiB ceiling, the one-hour redelivery window) the value comes from the document that owns the behaviour, not from this one.
