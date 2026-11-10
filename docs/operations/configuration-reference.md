---
document_id: DOC-OPS-007
title: Configuration Reference
product: Marites
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

## 2. The variables

| Variable | Type | Default | Required | What it does |
|---|---|---|---|---|
| `MARITES_URL` | URL | — | **Yes**, in any non-local deployment | The public base URL. Used for the OAuth redirect, for links in proposals, and for the `audience` check on tokens. A wrong value produces a login loop, not an error |
| `MARITES_LISTEN` | `host:port` | `:8080` | No | Where the HTTP API listens. The only port a user needs |
| `MARITES_ADMIN_ADDR` | `host:port` | `:9090` | No | Health and metrics. Must **not** be published to the user network |
| `MARITES_AUTH_MODE` | `entra` \| `none` | `entra` | No | `none` disables identity and is accepted **only** when `MARITES_LISTEN` is loopback — a guard, not a suggestion |
| `MARITES_ENTRA_TENANT_ID` | GUID | — | When `AUTH_MODE=entra` | The directory that issues tokens |
| `MARITES_ENTRA_AUDIENCE` | string | — | When `AUTH_MODE=entra` | The audience a token must carry; a token for another app is refused |
| `MARITES_BOOKKEEPING_PATH` | path | `./data/marites.db` | No | Marites' own store — config, audit, evidence, operations. Created if absent (DEC-012) |
| `MARITES_MESSAGE_STORE` | connection string | — | No | The adopter's message store. **Absent means live-only**: no counts, no message list, no search, and full recovery (DEC-016) |
| `MARITES_MIGRATION_CONNECTION` | connection string | — | Only for `marites migrate` | The DDL role, used briefly and never by the running service (DEC-014) |
| `MARITES_STORAGE_CEILING_BYTES` | integer | `53687091200` (50 GiB) | No | The ceiling `ERR-QUE-003` enforces and the sweeper pauses at |
| `MARITES_PARTITION_INTERVAL` | `daily` \| `hourly` | `daily` | No | Partition width for `message_capture`. Changing it affects new partitions only |
| `MARITES_EVIDENCE_MAX_ROWS` | integer | `10000` | No | The per-operation evidence bound. Beyond it the audit row records `payloadTruncated` (DEC-018) |
| `MARITES_REDELIVERY_WINDOW_SECONDS` | integer | `3600` | No | How long `capture_dedupe` holds an identity. Shorter than the broker's redelivery window under-counts (ADR-019) |
| `MARITES_CAPTURE_BATCH_ROWS` | integer | `500` | No | Rows per `COPY`. Lowering it costs throughput; raising it costs memory |
| `MARITES_LOG_LEVEL` | `debug` \| `info` \| `warn` \| `error` | `info` | No | `debug` logs message **headers**, never bodies |

**Two of these change behaviour rather than tuning it.** `MARITES_MESSAGE_STORE` decides whether the install can look back; `MARITES_AUTH_MODE=none` decides whether it has identity at all. Everything else is a dial.

## 3. Values that are not variables

Some things look configurable and deliberately are not:

| Not configurable | Why |
|---|---|
| Schema name | Fixed at `marites` in the message store; a configurable schema breaks every grant in [storage prerequisites](storage-prerequisites.md) |
| Migration order | Applied from the embedded set, in order, exactly once (DEC-014) |
| Evidence retention | Tied to the capture clock by decision, not by setting (DEC-017) |
| Audit retention | Permanent. There is no setting that shortens it, and that is the point |
| Tenant model | One implicit tenant in single-broker mode, row-level policy in multi-tenant (ADR-014) |

## 4. Startup validation

The service validates its configuration **before** it binds a port, and refuses to start rather than running half-configured:

| Condition | Result |
|---|---|
| `AUTH_MODE=none` with a non-loopback `MARITES_LISTEN` | Refuses to start; the message names the address |
| `AUTH_MODE=entra` with a missing tenant or audience | Refuses to start |
| `MARITES_MESSAGE_STORE` present but unreachable | Starts, **live-only**, and says so on every degraded screen — a store that is down must not stop recovery |
| `MARITES_MESSAGE_STORE` present with a schema version older than the binary | Refuses to start; `ERR-MIG-001`, naming the version |
| `MARITES_BOOKKEEPING_PATH` unwritable | Refuses to start. There is no audit without it, and no recovery without an audit |

**The asymmetry is deliberate.** A missing message store degrades; a missing bookkeeping store is fatal. One is history, the other is the record that the product exists to keep.

## 5. Changing a value

Most are read once at start; changing them requires a restart, and the service logs the values it started with so a drift between the compose file and the running process is visible.

Two are read per request — `MARITES_URL` and `MARITES_LOG_LEVEL` — because they are safe to change and annoying to restart for.

## 6. Document control

Revision 0.1, 10 November 2026. The five previously-named variables are recorded as they were used; the rest are named here for the first time and are therefore **specification, not description** — no binary reads them yet. Where a default is a judgement rather than a constraint (the 50 GiB ceiling, the one-hour redelivery window) the value comes from the document that owns the behaviour, not from this one.
