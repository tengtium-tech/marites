# Marites — RabbitMQ Message Monitor

> **SUPERSEDED — archived 07 November 2026.** This single-document plan was split into the specification set in `docs/`. It is retained for history only and is **not** implementation authority. Where it disagrees with `docs/`, the specification set wins. Its content is fully absorbed: requirements → DOC-SRS-001, architecture → DOC-ARCH-001, data → DOC-DATA-001, deployment → DOC-OPS-001, UI → DOC-UX-001, decisions → ADR-REGISTER and DECISION-LOG.

**Status:** Plan for review. No implementation started.

> **The name.** *Marites* is Filipino slang for the neighbour who knows everyone's business — which is precisely the job of a service that sees every message crossing your broker. Used as a proper noun for the product; lowercase for the binary, package identifiers, and resource prefixes.

---

## 1. What we're building

A web application that observes every message crossing a RabbitMQ broker — live and dead-lettered — and lets operators inspect, replay, discard, and publish messages from a single UI.

Three defining requirements from discovery:

| Requirement                                                          | Consequence                                                                             |
| -------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| **Continuous tap capture** — exact counts for all traffic               | Two tiers: rollups for counts, bounded rows for inspection (4.12)          |
| **Full operations** — peek, view, replay, discard, publish           | Write path to the broker exists. Requires auth, audit, confirmation, rate limits.       |
| **Any deployment** — single broker → multi-broker → K8s multi-tenant | Broker topology is data, not config. Tenancy is a first-class column.                   |
| **Multiple frontends, multiple backends**                            | The HTTP contract is the product boundary. C# is implementation #1, not the definition. |

---

## 2. Prerequisites and onboarding — you bring the broker

**Marites is a client. It never provisions, hosts, upgrades, or repairs a RabbitMQ broker.** Anyone deploying or forking this project must already have a running, correctly configured RabbitMQ instance *before* the app does anything useful. This section is a hard prerequisite, not a suggestion — a misconfigured broker produces an app that looks broken.

### 2.1 A running RabbitMQ broker (you supply it)

| You can use                                    | Notes                                                                  |
| ---------------------------------------------- | ---------------------------------------------------------------------- |
| Self-hosted RabbitMQ                           | 3.12+ or 4.x. AMQP 0-9-1.                                              |
| Docker / Compose                               | Fine for local evaluation; must still enable the management plugin.    |
| CloudAMQP, Amazon MQ, Cloudsmith-style managed | Management API availability varies — see 2.2.                          |
| Azure VM / AKS container                       | Works; Azure does **not** offer managed RabbitMQ, you run it yourself. |
| Kubernetes via the RabbitMQ Cluster Operator   | Recommended for the K8s profile.                                       |



> **Terminology note.** "Service Bus" is ambiguous. If you mean **Azure Service Bus**, that is a different product speaking AMQP 1.0 with a different management model — it is **not** compatible with Marites and is an explicit non-goal for v1. If you mean *"a message broker running as a managed service"*, then any RabbitMQ hosting qualifies and this document applies. Please confirm which you meant before we build.

### 2.2 Required broker configuration

Adopters must complete all of these. None are optional for full functionality.

- [ ] **Management plugin enabled** — `rabbitmq-plugins enable rabbitmq_management`. The app polls `/api/queues`, `/api/exchanges`, `/api/bindings` for all metadata. Without it there is no dashboard, only captured payloads.
- [ ] **AMQP port reachable** — 5672 (plain) or 5671 (TLS).
- [ ] **Management port reachable** — 15672, ideally behind HTTPS. Must be reachable from the app, not just from a browser.
- [ ] **A vhost** — a dedicated vhost per environment is strongly recommended over sharing `default`.
- [ ] **A dedicated user with least privilege**, not `guest` and not an administrator:
  - `monitoring` tag — read connections, channels, queues
  - `configure` + `write` on `^marites\.` — needed to declare and bind the shadow queues that make capture possible
  - `read` on the source queues and exchanges being watched
  - `write` on target exchanges — only if replay/publish is enabled
- [ ] **TLS** on AMQP and management for anything outside localhost. Broker credentials cross the wire on every poll.
- [ ] **`rabbitmq_tracing` plugin** — only if you need the firehose fallback in section 3C. Off by default; it is expensive.

**Managed-broker caveats.** Amazon MQ for RabbitMQ restricts parts of the management API and blocks some topology operations, which can disable continuous capture entirely. CloudAMQP allows it with per-plan limits. Verify against your provider *before* committing to Strategy A.

### 2.3 Dead-letter topology must already exist

This is the requirement people most often miss. **The app observes dead lettering — it does not create it.**

For dead-letter monitoring to show anything, the adopter must have already configured, on their own queues:

- [ ] `x-dead-letter-exchange` set on each source queue
- [ ] A DLQ declared and bound to that dead-letter exchange
- [ ] `x-dead-letter-routing-key` where the DLX is not a fanout
- [ ] A consumer policy that actually rejects/nacks — otherwise nothing ever dead-letters

If a queue has no DLX, the app **detects and warns** but will not fix it. Changing queue arguments requires deleting and redeclaring the queue, which destroys messages — we will never do that automatically, and the UI will say so explicitly.

### 2.4 Network and resource budget

- **Network:** app → broker on AMQP and management ports, both directions for acks. In Kubernetes this means egress rules plus any network policy on the broker namespace.
- **Broker disk:** each shadow queue is bounded by `x-max-length × avg message size`. Reserve that per watched queue. A monitor must never be the reason a broker fills its disk.
- **App disk:** `retention_days × daily message volume × avg size`, plus spillover for payloads over 64 KB. Budget this before enabling capture on a high-volume queue.

### 2.5 Preflight readiness check (M0 deliverable)

Documentation drifts; a checklist that runs does not. The app ships a preflight endpoint — `POST /api/v1/brokers/{id}/preflight` — returning pass/fail per item:

| Check                                                                     | Failure means                            |
| ------------------------------------------------------------------------- | ---------------------------------------- |
| Management API reachable and authenticated                                | No dashboard at all                      |
| Vhost visible to the credentials                                          | Wrong user or vhost                      |
| `configure`/`write` on `marites.` prefix (declare + delete a probe queue) | Capture impossible — Strategy B/C only   |
| Each selected queue reports a DLX                                         | Dead-letter monitoring will show nothing |
| Shadow queue declarable with bounded args                                 | Broker policy blocks topology writes     |
| Clock skew app ↔ broker < 5 min                                           | `x-death` timestamps will be wrong       |

The React app surfaces this as a setup wizard that must pass before a broker profile can be saved. Adopters get a precise error instead of a dashboard full of zeros.

### 2.6 Deploying the app

Once 2.1–2.4 are satisfied, the app deploys on **the adopter's own server**. It is a single self-contained service — no broker sidecar, no agent to install on the RabbitMQ host, no privileged access required beyond the credentials in 2.2.

| Deployment shape                     | How                                                             | Suited to                                     |
| ------------------------------------ | --------------------------------------------------------------- | --------------------------------------------- |
| **Docker Compose**                   | `docker compose up -d` with a `.env` file                       | Single server, evaluation, small teams        |
| **Container on your infrastructure** | `marites` image + Helm chart (K8s) or plain manifest (ECS / VM) | Production, multi-broker, multi-tenant        |
| **Self-contained binary**            | `dotnet publish -r <rid> --self-contained`                      | Air-gapped environments, no container runtime |

**Network direction:** the app needs **outbound** access to the broker on the AMQP and management ports (2.4). It needs **no inbound** access to the broker, and the broker needs no inbound access to the app. Only the app's own HTTP port is exposed to users. This matters for security review: nothing new is reachable from the broker's network.

### 2.7 Pointing it at your broker

The app ships with **zero broker configuration baked in**. Connection details are runtime data, not build-time config — a **broker profile**:

| Field          | Example                                          | Notes                                                                 |
| -------------- | ------------------------------------------------ | --------------------------------------------------------------------- |
| AMQP URI       | `amqps://marites:••••@rabbit.internal:5671/prod` | 5671 + TLS for anything non-local                                     |
| Management URI | `https://rabbit.internal:15672`                  | Required for metadata; may be a different host from AMQP              |
| vhost          | `/prod`                                          | Must already exist — the app does not create vhosts                   |
| Credentials    | the least-privilege user from 2.2                | Never `guest`, never administrator                                    |
| TLS options    | CA bundle, hostname verification                 | Self-signed internal CAs are common; verification stays on by default |

Two ways to supply it:

1. **UI setup wizard** (recommended) — guided form that runs preflight inline and refuses to save a profile that fails. Best for first-time adopters.
2. **Config file / environment variables** (prefix `MARITES_`) — for GitOps and infrastructure-as-code. Secrets come from the environment or a secret manager and are never committed to the repo.

**Multiple brokers are first-class.** Add as many profiles as needed — each with its own vhost, credentials, and capture rules. One app instance can watch a dev broker and a production broker side by side; the dashboard groups by broker profile.

**Secret handling:** credentials are encrypted at rest (AES-GCM, key from env/KMS), never returned by the API, never written to logs, and masked in the UI. AMQP URIs pasted with embedded passwords are redacted on read.

### 2.8 End-to-end first run

```
Deploy  →  Open UI  →  Add broker profile  →  Preflight  →  Pick queues
                                                              ↓
                            Dashboard live  ←  Capture bound + backfill
```

1. Deploy the container (or binary) on your server; expose the HTTP port.
2. Open the UI — first run shows the setup wizard, not an empty dashboard.
3. Enter the broker profile (2.7). Preflight runs immediately (2.5); fix anything red.
4. Select the queues and exchanges to watch. The app pre-selects queues that already report a DLX, since those are the ones worth monitoring.
5. Confirm — capture bindings are declared and a bounded backfill is offered for what's already in the queues.
6. Dashboard goes live within one poll interval (~10 s).
7. Repeat 3–5 for each additional broker; add users and roles when moving past the single-user profile.

**Time to first value: minutes**, assuming section 2.2 is satisfied. If preflight fails on topology permissions, the app still starts in **read-only metadata mode** — a degraded but genuinely useful dashboard rather than a hard failure.

---

## 3. The hard constraint you need to accept up front

**AMQP has no peek.** A queue is a destructive read stream: consuming a message removes it. There is no "read without consuming" primitive, and two consumers on one queue are *competing* consumers — they split messages, they don't both see them.


So "monitor every message regardless of dead letter" cannot be done by attaching a listener. It must be done by one of three strategies:

### Strategy A — Shadow bindings (primary)
Declare a monitor-owned queue and bind it to the same exchange with the same routing key. RabbitMQ fans out to both queues, so production consumers are untouched and we receive a full copy.

- **Captures:** every message published *after* the binding exists.
- **For DLQs specifically:** bind directly to the dead-letter exchange. This is the cleanest signal — we see the dead lettering event itself, plus `x-death` headers telling us *why* (rejected / expired / maxlen / delivery_limit).
- **Cost:** writes to broker topology. Needs `configure`+`write` on the vhost. Not always possible on managed brokers (Amazon MQ restricts management API; CloudAMQP allows but limits).
- **Safety:** every shadow queue is declared with `x-max-length` + `x-message-ttl` + its own DLX. A monitor must never be the reason a broker runs out of disk.

### Strategy B — Management API peek (backfill)
`POST /api/queues/{vhost}/{name}/get` with `ackmode=reject_requeue_true` pulls message bodies and puts them back. This is the only way to see what is *already sitting* in a queue before we bound to it.

- **Cost:** blocking and slow. Disrupts ordering. Must be rate-limited and user-initiated, never on a timer.
- **Use it for:** one-shot backfill when a queue is added, and the "inspect this queue now" button.

### Strategy C — Firehose tracing (fallback)
`rabbitmq_tracing` plugin logs publish/deliver events broker-wide. No topology changes, but it's a global file-based firehose — heavy, unsearchable, needs broker file access. Offer as a last resort for brokers where A and B are blocked.

**Decision required from you:** Strategy A needs topology write permission. Confirm the brokers you'll point this at allow declaring queues and bindings. If they don't, we fall back to B+C and lose continuous capture.

### 3.D Where the tap lives — one hop, not three

A common instinct is to place a **second RabbitMQ cluster** between the adopter's broker and Marites: *main cluster → `.Tap` cluster → Marites API → Postgres.* The instinct — that something must copy messages out — is correct. The mechanism is not, for four reasons.

**1. RabbitMQ has no "point at another cluster" tap.** Native clustering is not a bridge: it fuses several nodes into one logical broker, requiring a shared Erlang cookie and a low-latency network. You cannot cluster two independently operated clusters across a network boundary and get tap behaviour. The actual cross-cluster mechanisms are:

- **Federation** — a downstream exchange pulls copies from an upstream exchange, non-destructively. This is the only mechanism that genuinely does what "tap cluster" implies.
- **Shovel** — moves messages from a source to a destination. From a queue it is destructive; safe only from an exchange, or from a queue nothing else consumes.

So the design is *possible* via Federation. It just costs a second RabbitMQ cluster to install, patch, upgrade, back up, and page someone about.

**2. It adds failure points to the thing whose job is to detect failure.** `main → .Tap → API → DB` has three places to break silently. A monitor that quietly stops monitoring is worse than no monitor, because people trust it. Every hop needs its own liveness signal and lag metric — you end up monitoring the monitor.

**3. Routing ingestion through the HTTP API is the wrong shape.** If `.Tap` POSTs messages to Marites, the API becomes a high-throughput ingest tier: an HTTP hop per message, extra serialisation, and a web tier sized for write throughput instead of for users. The write path should be worker → Postgres, directly.

**4. It isn't needed.** The capture worker is already a tap. It connects to the adopter's broker as an ordinary AMQP client, declares a bounded shadow queue, binds it to the exchange, and consumes the copy — non-destructive, no second broker (Strategy A):

```
Adopter's broker  ──AMQP──▶  Marites capture worker  ──COPY──▶  Postgres
        ▲                                                          │
        └────────────── Marites API ──── reads ────────────────────┘
```

Note what Marites actually requires: **AMQP and management access to whichever broker holds the queues.** It does not care what publishes into that broker or what the broker is fed by upstream — if a "service bus" delivers into the RabbitMQ cluster, that is upstream of Marites and invisible to it.

### When a separate hop *is* justified

Three cases, all exceptional:

| Situation | Use |
|---|---|
| Managed broker blocks topology writes (e.g. Amazon MQ) | Federate the upstream exchange into a small broker you control, then tap that |
| Broker and Marites sit in different network zones | A **relay** — one container, not a cluster. Consumes, batches, pushes over HTTPS with a disk-backed buffer |
| Marites may be down for long windows and captures cannot be lost | Same relay, buffer sized to the outage you must survive |

In every case the added component is a **relay**, not a RabbitMQ cluster: one process, no Erlang, no quorum queues, no cluster upgrade path, no split-brain.

### Non-negotiable if any hop is added

The pipeline must expose **lag and last-seen timestamps per hop**, and the dashboard must raise a **stale-capture warning** when a queue's capture stops advancing while its depth keeps growing. Without that signal, a broken tap is indistinguishable from a quiet system — which is the one failure mode a monitoring product cannot afford.

---

## 4. Solution architecture

```
┌──────────────────────────────────────────────────────────────┐
│  React SPA  (frontend #1 of N)                               │
│  React 19 + Vite + TanStack Query + shadcn/ui                │
│  generated API client from OpenAPI — no hand-written types   │
└───────────────────────────┬──────────────────────────────────┘
                            │  HTTPS + SSE/SignalR
┌───────────────────────────▼──────────────────────────────────┐
│  API layer  (implementation #1 of N: ASP.NET Core / .NET 9)  │
│                                                              │
│   /api/v1/capabilities   ← UI adapts to backend features     │
│   /api/v1/brokers        ← connection profiles + preflight   │
│   /api/v1/queues         ← metadata, depths, rates           │
│   /api/v1/messages       ← captured messages, search         │
│   /api/v1/operations     ← peek / discard / replay / publish │
│                                                              │
│  ┌────────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ Capture workers│  │ Mgmt API     │  │ Ops executor     │  │
│  │ (RabbitMQ      │  │ poller       │  │ (rate-limited,   │  │
│  │  .Client 7.x)  │  │ (HTTP)       │  │  audited)        │  │
│  └───────┬────────┘  └──────┬───────┘  └────────┬─────────┘  │
└──────────┼──────────────────┼───────────────────┼────────────┘
           │ AMQP 0-9-1       │ Mgmt HTTP         │ AMQP
┌──────────▼──────────────────▼───────────────────▼────────────┐
│  RabbitMQ broker(s)  — 1..N, any vhost, on-prem or cloud     │
│  ⚠ supplied and configured by the adopter — see section 2    │
└──────────────────────────────────────────────────────────────┘
           │
┌──────────▼───────────────────────────────────────────────────┐
│  Message store  — Postgres (prod) / SQLite (single-node)     │
│  partitioned by day · payload spillover to disk/S3 >64 KB    │
└──────────────────────────────────────────────────────────────┘
```

### Why contract-first matters here

You plan C#, then Go, then Rust. Three backends will drift unless the contract is executable. Therefore:

1. **`07-data-and-contracts/openapi/openapi.yaml` is the source of truth.** It is written and reviewed before any C# code.
2. **Frontend types are generated** (`openapi-typescript` + `orval`) — never hand-maintained.
3. **A black-box conformance suite** (`contract-tests/`) runs identical HTTP tests against any backend. Go isn't "done" until it passes the same suite C# passes.
4. **`GET /api/v1/capabilities`** returns a feature list. When the Go backend ships without replay, the UI hides the button instead of 500-ing. This is what makes partial backends viable.

The C# backend stays thin and boring on purpose: routing, validation, orchestration. No business logic that can't be reimplemented in Go in a week.

### 4.1 Clean Architecture or vertical slices?

**Recommendation: vertical slices as the organizing principle, with Clean Architecture's dependency rule applied at exactly three seams. Not a four-project onion.**

Why the full onion doesn't pay for itself here:

- Its central promise is *swappable infrastructure*. In this system only one infrastructure is genuinely swappable — **the broker**. Postgres is a commitment you've just made, not a choice, so `IRepository<T>` over EF Core is ceremony. And the HTTP boundary is already protected by the OpenAPI contract, not by interfaces: the Go and Rust implementations are in *different languages*, so a C# interface cannot help them. The contract is the abstraction.
- `IQueueService` with twelve methods, a service per entity, and a DTO mapping layer per layer means every future change touches five files. That cost is paid on every commit for the life of the project.
- It optimizes the wrong axis. Your real seams in this system are **lifecycle and risk**, not technical layer: background capture behaves nothing like request/response, and topology mutation is dangerous in a way that reading a queue is not.

Where dependency inversion genuinely earns its keep — three ports, no more:

| Port | Why it is a real seam |
|---|---|
| `IBrokerAdmin` | Management API is HTTP and mockable; Amazon MQ and CloudAMQP differ. Preflight tests need a fake. |
| `IMessageTap` / `IMessagePublisher` | AMQP client specifics. Capture must be testable without a live broker. |
| `IClock` | `x-death` timestamps, retention sweeps, confirm-token TTL — all clock-dependent, all untestable otherwise. |

Everything else talks to concrete types. **Clean Architecture is a dependency rule, not a project count.**

### 4.2 Module map

```
src/
  Marites.Contracts/     # OpenAPI-generated DTOs, enums, error codes. Zero dependencies.
  Marites.Domain/        # ~6 types: BrokerProfile, QueueRef, CaptureRule, MessageCapture,
                         # OperationKind, Role. Behaviour only where it prevents a bug.
  Marites.Brokers/       # Ports: IBrokerAdmin, IMessageTap, IMessagePublisher.
                         # RabbitMQ adapter. The ONLY project referencing RabbitMQ.Client.
  Marites.Storage/       # DbContext, migrations, COPY-based bulk writer, partition sweeper.
  Marites.Operations/    # Guard chain: authorize → validate → dry-run → confirm → rate-limit → execute → audit.
  Marites.Capture/       # Worker logic: topology setup, consume loop, batching, backpressure.
  Marites.Identity/      # Entra ID JWT validation, role→capability mapping, authorization policies.
  Marites.Api/           # Minimal API endpoints, one folder per slice. Composition root.
  Marites.Worker/        # Capture host. Same image, different entrypoint.
tests/
  Marites.UnitTests/
  Marites.IntegrationTests/   # Testcontainers: real Postgres + real RabbitMQ
  contract-tests/             # Black-box HTTP conformance — runs against C#, Go, Rust alike
```

Nine projects, and the split is by **capability**, not by layer. `Operations` exists because replay/discard/publish share a guard chain and an audit requirement — that is a real boundary. There is no `Marites.Application` holding everything, because that is where onions rot.

### 4.3 Slice anatomy

One file per use case. Endpoint, handler, request, response, validation — together.

```
Marites.Api/Features/
  Queues/ListQueues.cs
  Queues/GetQueueDetail.cs
  Messages/SearchMessages.cs
  Messages/GetMessagePayload.cs
  Operations/ReplayMessages.cs
  Operations/PurgeQueue.cs
  Brokers/AddBrokerProfile.cs
  Brokers/RunPreflight.cs
```

A slice may call `Marites.Storage` and `Marites.Operations` directly. It may not contain business rules that another slice needs — those move to `Domain` or `Operations`. The test for whether a slice is right-sized: *if you delete the feature, you delete one file.*

### 4.4 The rule that keeps it honest

One architecture test, using ArchUnitNET:

- `Marites.Api` must **not** reference `RabbitMQ.Client` or `Npgsql`
- `Marites.Brokers` and `Marites.Storage` must **not** reference `Marites.Api`
- `Marites.Contracts` must reference **nothing**

That single test delivers most of what a layered onion promises, and it fails the build the moment someone shortcuts the boundary. It is worth more than four projects of folder structure.

### 4.5 Read path

```
React (MSAL token) → /api/v1/... → slice handler → EF Core query
                   → Postgres → DTO from Marites.Contracts → JSON
```

Reads are thin. No domain logic in the query path; projections straight to contract DTOs so the UI shape is never an accident.

### 4.6 Capture path (write)

```
Worker host
  → topology setup (shadow queue + bindings, bounded)
  → AsyncEventingBasicConsumer, prefetch 200
  → normalize (x-death, redact, hash, inline-vs-spill)
  → Channel<CapturedMessage> buffer          ← decouples AMQP thread from DB
  → batch: 500 rows or 1 s
  → Npgsql binary COPY into message_capture  ← not EF Core
  → ack (only after durable write)
```

Two non-obvious choices:

- **`COPY`, not EF Core.** EF Core's batched insert tops out around a few thousand rows/sec with per-row change tracking. `BeginBinaryImport` does 50k+/sec. Capture is the one path where this matters.
- **`Channel<T>` between consumer and writer.** RabbitMQ's dispatch thread must never block on Postgres, or the broker's flow control kicks in and you've made the broker's problem your problem.

### 4.7 Data access strategy

| Concern | Choice |
|---|---|
| CRUD (brokers, rules, users) | EF Core 9 + Npgsql |
| Message capture | Raw binary `COPY`, batched |
| Retention | Native `RANGE` partitioning on `captured_at`, one partition per day |
| Partition lifecycle | Sweeper creates partitions 7 days ahead, drops those past retention |
| Header queries | `headers_json` as `JSONB` + GIN index |
| Payload | Inline `bytea` up to 64 KB (TOAST handles it); larger spills to object store |
| Text search | `pg_trgm` on `routing_key` / `message_id` at M3; full-text on payload only if demanded |

**Throughput reality check.** 5,000 msg/s at 1 KB is ~430 GB/day of payload, and ~100 GB/day even as metadata-only rows. Storing a row per message for everything is not survivable at that rate, yet exact counts *are* required. Those two needs are separable — see 4.12.

### 4.8 Identity, roles, capabilities

**Entra ID** (OIDC), with the React app using MSAL.js (auth code + PKCE) and the API validating JWT bearer tokens. Roles are declared as **App Roles** on the Entra app registration and arrive in the `roles` claim — cleaner than security groups, which require a Graph call per request.

Roles map to **capabilities**, not to endpoints. Authorization is policy-based, and the role→capability table lives in exactly one file (`Marites.Identity`):

| Capability policy | Viewer | Support | Admin |
|---|---|---|---|
| View queues, depths, rates, DLQ badges | ✓ | ✓ | ✓ |
| View message payloads | ✓ | ✓ | ✓ |
| Peek / backfill a queue | — | ✓ | ✓ |
| Discard a single message | — | ✓ | ✓ |
| Replay (rate-limited, dry-run enforced) | — | ✓ | ✓ |
| Publish arbitrary message | — | — | ✓ |
| Purge a queue | — | — | ✓ |
| Manage broker profiles | — | — | ✓ |
| Manage capture rules | — | — | ✓ |
| Assign roles | — | — | ✓ |
| View audit log | — | ✓ | ✓ |

Three points that will bite if left implicit:

- **Viewer seeing payloads is a policy decision, not a default.** Payloads routinely contain PII and tokens. If Viewer should see redacted bodies only, that changes the message endpoint's contract — decide it before M0, not after.
- **Break-glass admin is mandatory for self-hosted.** When Entra is unreachable or misconfigured, an on-prem install must still be administrable. A local admin account, disabled by default, enabled by env var, with its use written to the audit log.
- **`MARITES_AUTH_MODE=none|entra`.** The single-server profile needs no IdP; forcing Entra on a local evaluation would kill adoption. `none` is localhost-only and refuses to start if bound to a non-loopback address.

### 4.9 Process topology

One image, three entrypoints:

| Command | Runs | Used by |
|---|---|---|
| `marites api` | HTTP only | K8s Deployment (N replicas behind a Service) |
| `marites worker` | Capture only | K8s Deployment (scaled by shard count) |
| `marites all` | Both in one process | Docker Compose, single server |

Splitting is a *deployment* concern, not a code concern — same assemblies either way. Capture replicas claim queue shards via Postgres advisory locks, so scaling out is safe and no queue is double-captured.

### 4.10 Why not microservices

One team, one database, and capture writes to the same store the API reads. Splitting now would buy you network partitions and distributed transactions in exchange for nothing. The worker boundary in 4.9 is already the seam you'd cut along if capture ever outgrows the API — and because `Marites.Capture` has no dependency on `Marites.Api`, that cut stays cheap.

### 4.11 Multiple clusters

Adopters routinely run more than one RabbitMQ cluster — prod and staging, per-region, per-team, or legacy alongside new. That does **not** add hops. It means Marites is a client of **N clusters in a star**, never a chain:

```
          ┌── prod cluster ──────┐
Marites ──┼── staging cluster ───┤   one hop per cluster,
          └── eu-west cluster ───┘   independent connections
```

Each cluster gets its own `broker_profile` row (AMQP URI, management URI, vhost, credentials, TLS, **environment tag**), its own preflight result and health state, and its own capture worker with its own connection. That last point is deliberate — **bulkhead isolation**. One unreachable or slow cluster must not stall the workers, or apply backpressure, on the others.

**Identity is never the queue name.** `orders` exists in prod and staging simultaneously. Every key is `(broker_profile_id, vhost, queue_name)`, and every captured row carries `broker_profile_id`. Anything keyed on queue name alone is a latent bug.

**Environment tags are a safety feature, not cosmetics.** A Support user replaying 200 messages while believing they are in staging is more common than anyone admits. Every operation confirmation must display cluster and environment, prod must be visually distinct in the UI, and replay against `prod` should require typing the environment tag — the same typed-confirmation pattern used for purge.

**The dedupe trap.** If their clusters are linked by Federation or Shovel, the *same message* exists in more than one cluster and Marites will capture it once per cluster. Two policies, and this must be an explicit decision:

- **Scope by cluster (default)** — each cluster's view is independent. Cross-cluster duplicates are expected and shown as such.
- **Global dedupe** — group by `(message_id, content_hash)` across brokers and display provenance ("seen in prod and eu-west").

Default is scoping, because aggressive cross-cluster dedupe can mask a genuine double-publish — which is exactly the incident someone opened the dashboard to find.

**Storage:** one Postgres for all clusters, `broker_profile_id` on every captured row, indexed alongside `captured_at`. Do not give each cluster its own database — cross-cluster comparison is the main reason people want a single dashboard.

**Practical ceiling:** dozens of clusters, not hundreds. Each cluster costs a connection pool, a poller, and a worker. Beyond roughly fifty, add regional relays that aggregate per zone — the same relay from 3.D, not a second broker.

### 4.12 Volume accounting — counting is not storing

**The requirement:** exact counts of dead-lettered versus healthy traffic, per queue, over time. That is a counting problem.

**The trap:** "capture everything" is usually read as "write a row for every message". At 5,000 msg/s that is 432 million rows per day — roughly 100 GB/day of metadata alone before any payload. No retention window fixes that, and the dashboard queries get slower every day you run it.

Counting and storing are separable, and the product needs them separated:

| Layer | What it holds | Coverage | Retention |
|---|---|---|---|
| **`message_rollup`** | Per-queue, per-minute counters: published, dead-lettered, bytes, breakdown by `death_reason` | **100%** — every message, exactly | Long (months) |
| **`message_capture`** | One row per message: headers, routing key, size, `x-death`, payload per policy | Bounded window | Short (hours–days) |

The rollup is written from the same in-memory batch the capture worker already builds — counters incremented per message, flushed once per second. It costs a few hundred bytes per queue per minute. A hundred queues for a year is single-digit gigabytes. **Exact counts, forever, for almost nothing.**

Row-level capture then becomes a bounded investigation window rather than a permanent archive. `payload_policy` per queue — `full`, `sampled`, `none` — governs payload retention only. **Counting is never affected by payload policy.** A queue set to `none` still reports perfect counts.

### The double-counting rule

A dead letter is the **same message observed twice**: once entering its original exchange, once arriving at the DLX. If both are captured, the naive total counts it twice.

Therefore these are separate series and must never be summed:

- `published` — messages routed into an exchange
- `dead_lettered` — messages arriving at a DLX, split by reason: `rejected`, `expired`, `maxlen`, `delivery_limit`
- `dead_letter_rate` — `dead_lettered / published` over the same window

`messages_seen` is a **pipeline health metric**, not a business metric. It belongs on an internal ops page, never on the dashboard where someone will add it to something.

Rows are linked rather than deduplicated: the dead-letter row carries `origin_capture_id`, matched by `message_id` (falling back to `content_hash` plus routing key within a time window). The link is best-effort and nullable — provenance is a bonus, never a correctness dependency for counts.

### Capturing non-dead-letter traffic

Counting everything requires shadow bindings on **exchanges**, not only on DLXs. For a topic exchange, a single `#` binding captures 100% of routed messages. That is simple and complete, and it is also the highest-volume option — which is precisely why the rollup tier exists and why `payload_policy` defaults to `none` on firehose exchanges.

### 4.13 Dashboard metrics — what the numbers mean

The overview shows four numbers, defined once here so the UI and the API cannot drift apart:

| Tile | Source | Definition |
|---|---|---|
| **Messages published** | `rollup.published` | Messages routed into a watched exchange, in the window |
| **Dead letters** | `rollup.dead_lettered` | Messages arriving at a DLX, in the window |
| **Dead-letter rate** | derived | `dead_lettered / published`, same window |
| **Peak rate** | `rollup` | Highest messages/second observed in the window |

Four rules, each of which prevents a specific confusion:

1. **Never show `published + dead_lettered` as a "total messages" figure.** They overlap by construction — a dead letter is a published message seen a second time (4.12). The combined number is a pipeline metric and belongs on an internal ops page, not the dashboard.
2. **Never show a dead-letter count without its rate.** "500 dead letters" is alarming at 600 msg/min and unremarkable at 600,000. The rate is the interpretable number; the count is the context.
3. **Always break the dead-letter tile down by reason** — `rejected`, `expired`, `maxlen`, `delivery_limit`. "500 dead letters" is a different incident depending on whether a consumer is failing or a queue hit its length limit.
4. **Zero must be distinguishable from blind.** A dashboard reading `0` is ambiguous: either nothing is flowing, or capture has stopped. The overview needs a separate capture-health indicator (last message seen per queue, with a stale badge) so "quiet" and "broken" never look identical.

The per-queue table below the tiles carries `published`, `dead_lettered`, `DL rate`, current backlog, and last-seen. Backlog comes from the metadata poller, not the rollup — one is live broker state, the other is historical volume, and mixing them is how dashboards start lying.

---

## 5. Data model

Multi-tenant from day one — retrofitting tenancy is a rewrite.

```sql
tenant            (id, name, slug, created_at)
broker_profile    (id, tenant_id, name, environment, amqp_uri_enc,
                   mgmt_uri, credentials_enc, vhost, tls_opts,
                   is_default, preflight_state, preflight_at)
queue_ref         (id, broker_profile_id, name, vhost, is_dlq,
                   dlx_name, bound_at, capture_enabled)
capture_rule      (id, queue_ref_id, bind_pattern, enabled,
                   payload_policy, row_retention_hours, max_rows, max_bytes)
message_rollup    (queue_ref_id, bucket_start, published, dead_lettered,
                   bytes, death_reasons_json)   -- PK (queue_ref_id, bucket_start)
message_capture   (id, tenant_id, broker_profile_id, queue_ref_id,
                   exchange, routing_key, message_id, correlation_id,
                   headers_json, payload_ref, payload_size, content_type,
                   content_hash, captured_at, is_dead_letter,
                   death_reason, death_count, origin_capture_id, replay_of_id)
replay_job        (id, tenant_id, queue_ref_id, state, total, processed,
                   failed, halt_reason, created_by, created_at, updated_at)
operation_audit   (id, tenant_id, actor_id, action, target, params_json,
                   result, message_count, created_at, ip)
app_user          (id, tenant_id, entra_object_id, email, role,
                   is_break_glass)   -- viewer|support|admin
```

Notes that matter:

- **`credentials_enc`** — broker credentials encrypted at rest (AES-GCM, key from env/KMS). Never logged, never returned by the API.
- **`preflight_state` / `preflight_at`** — last readiness result. A broker profile cannot be enabled for capture until preflight passes; re-running it is a button, not a migration.
- **`payload_ref`** — inline up to 64 KB; larger payloads go to disk/S3 with only a reference in the row. Prevents a 50 MB message from wrecking a table scan.
- **`content_hash`** — SHA-256 of payload + key headers. Gives dedupe and "this same message appeared 400 times" grouping, which is the single most useful view when a DLQ is flooding.
- **`is_dead_letter` / `death_reason`** — parsed from `x-death` on ingest. This is what turns "a pile of messages" into "1,204 rejected, 3 expired, 88 dropped for maxlen".
- **`origin_capture_id`** — links a dead letter back to the publish that produced it. Best-effort and nullable; counts never depend on it (4.12).
- **`message_rollup`** — the counting tier, and the reason long-range counts survive row expiry. It is written once per second from the live capture batch and is **never** backfilled from `message_capture`, because rows expire and counts must not. If rollups were derived from rows, every retention sweep would silently rewrite history.
- **`replay_job`** — replay is asynchronous, not a single request. Section 7 requires dry-run, per-minute rate limiting, and auto-halt when the target queue grows faster than it drains — all of which imply persisted state. Without this table, "auto-pause" is impossible and a restarted API forgets a replay is in flight.
- **Confirm tokens are deliberately *not* a table.** They are stateless HMAC-signed values with a 60-second expiry, so there is nothing to store and no cleanup job to write. A table here would add a write per operation and a sweeper for no benefit.
- **`replay_of_id`** — replay never deletes the original record; it links. Provenance stays intact.

Retention is enforced per capture rule by a background sweeper (day-partition drop + size cap), not by ad-hoc deletes.

---

## 6. Capture pipeline

Per enabled queue, a worker owns:

1. **Topology setup** — declare shadow queue `marites.{tenant}.{hash}` with `x-max-length`, `x-message-ttl`, own DLX; bind to source exchange with the source routing key (or to the DLX for dead-letter monitoring).
2. **Consume loop** — `AsyncEventingBasicConsumer` on a dedicated connection (separate from the ops connection, so a slow capture never blocks a replay). `prefetch` ~200, manual ack after a successful store write. Ack only after durability — losing a captured message is acceptable, but storing-then-acking-then-crashing must not duplicate.
3. **Normalise** — extract `x-death`, strip credentials from headers, hash, decide inline vs spillover.
4. **Batch insert** — 500 rows or 1 s, whichever first. Individual inserts will not keep up with a busy queue.
5. **Backpressure** — if the store lags, the worker stops acking and lets RabbitMQ buffer. A monitor that takes down the broker is worse than no monitor.

**Metadata poller** (independent, every 10 s): `/api/queues/{vhost}` for depth, rates, consumer counts, unacked. Cheap, cacheable, and drives the dashboard tiles and sparklines. Captured payloads are for investigation; the poller is for the live picture.

---

## 7. Operations and their safety model

Every write operation goes through one executor with a shared guard chain:

```
authorise (role + tenant) → validate (limits, target exists)
  → dry-run preview (count + sample, returned to UI)
  → confirm token (server-issued, 60 s TTL)
  → rate limit (token bucket per tenant)
  → execute → audit row → return result
```

| Operation | Mechanism | Guard |
|---|---|---|
| **Peek / view** | Store lookup, or Mgmt API get+requeue for uncaptured | Read-only, safe |
| **Discard** | Ack from shadow queue; or Mgmt API purge (destructive) | Preview count, typed queue-name confirmation |
| **Replay** | Republish to original exchange + routing key, `x-marites-replay-of` header, TTL guard | **Dry run first**, max N/min, optional target override, auto-pause if the queue grows during replay |
| **Publish** | Publish arbitrary message to exchange | Editor with JSON validation, required idempotency key |

Two non-obvious rules:

- **Replay must not loop.** A dead letter replayed back to its origin will dead-letter again if the consumer still rejects it. The dry run surfaces the death reason so the operator sees this *before* pressing the button. We also cap consecutive replays of the same `content_hash` and auto-halt the replay job if the target queue's depth climbs faster than it drains.
- **Never auto-purge.** No scheduled destructive operations, ever. Purge is always an explicit human action with a typed confirmation.

---

## 8. Deployment profiles

Same `marites` binary, three configurations — the difference is auth and scale, not storage engine.

| Profile | Store | Auth | Isolation |
|---|---|---|---|
| **Local / single broker** | Postgres (container via Compose) | `MARITES_AUTH_MODE=none`, loopback only | single implicit tenant |
| **Multi-broker** | Postgres | Entra ID + JWT | tenant column, capability policies |
| **K8s / multi-tenant** | Postgres (managed) | Entra ID, multi-tenant app registration | row-level tenant policy, per-tenant encryption keys |

**Postgres everywhere — SQLite is deliberately not supported.** Section 4.7 depends on binary `COPY`, native table partitioning, `JSONB`, and GIN indexes. All four are Postgres-only. A second provider would mean two code paths for the most performance-critical part of the system, and the "lightweight" profile would silently lose partitioning — which is the feature that keeps retention from becoming a DELETE storm. Local installs get Postgres in a container instead.

Ships as a Docker image + Helm chart. Capture workers scale horizontally by sharding `queue_ref` across replicas (each queue owned by exactly one worker, elected via Postgres advisory lock — no duplicate capture, no split-brain).



---

## 9. Milestones

| # | Deliverable | Exit criteria |
|---|---|---|
| **M0** | Contract + skeleton + preflight + deploy | `openapi.yaml` reviewed; React shell renders; C# serves `/capabilities` and `/brokers/{id}/preflight`; setup wizard blocks save on failure; `docker compose up` yields a working UI pointed at a test broker; CI green |
| **M1** | Metadata dashboard | Queue tree, depths, rates, DLQ badges, live sparklines. Read-only, single broker |
| **M2** | Capture pipeline | Shadow binding + backfill; rollups written every second; rows land in store; retention sweeper runs |
| **M3** | Message explorer | List, filter by routing key / death reason / correlation id, payload viewer, duplicate grouping |
| **M4** | Operations | Peek, discard, replay (dry-run + rate limit), publish. Full audit log |
| **M5** | Multi-broker + tenancy | Broker CRUD, encrypted creds, RBAC, per-tenant isolation tests |
| **M6** | Backend #2 readiness | Conformance suite green; Go backend passing M0–M1 endpoints |

M1 is genuinely useful on its own — if M2's broker permissions turn out to be blocked, you still have a working dashboard. M0 ships the preflight check specifically so adopters find out about a misconfigured broker on day one rather than after building a capture pipeline.

---

## 10. Decisions

### Settled

| Decision | Choice |
|---|---|
| Frontend | React 19 + shadcn/ui, API client generated from OpenAPI |
| Backend #1 | ASP.NET Core / .NET 9, contract-first, thin |
| Architecture style | Vertical slices + three ports (4.1) — **not** a four-project onion |
| Database | PostgreSQL, single provider, no SQLite (section 8) |
| Identity | Entra ID (OIDC), App Roles in the `roles` claim |
| Roles | Admin, Support, Viewer → capability policies (4.8) |
| Repo shape | Monorepo: `src/`, `tests/`, `contract/`, `apps/web/` |

### Still open — these block M0

1. **"Service Bus"** — Azure Service Bus (incompatible, out of scope) or RabbitMQ-as-a-managed-service? Changes the entire target.
2. **Viewer payload visibility** — should Viewer see message bodies, or redacted/summary only? This is a contract decision, not a toggle; it changes `GET /messages/{id}` before it is written.
3. **Broker permissions** — can the app's user declare queues and bindings? Gates continuous capture (Strategy A).
4. **Existing DLX topology** — do your queues already carry `x-dead-letter-exchange`, or must the app guide adopters through it?
5. **Expected volume** — msg/s on the busiest queue and max payload size. Decides whether `metadata_only` becomes the default rather than `full`.
6. **Payload sensitivity** — may payloads be stored verbatim, or do they need field-level masking / encryption at rest?
7. **MediatR or not** — my recommendation is **no**. Slices call handlers directly; a mediator adds indirection without buying anything at this size, and MediatR's licensing moved to a commercial model for larger organisations, which is worth verifying before adopting it as a dependency.
8. **Are your clusters federated or independent?** If Federation or Shovel links them, the same message appears in several clusters and capture will duplicate it. Decide between per-cluster scoping (default) and global dedupe (4.11).

---

## 11. Explicit non-goals (v1)

- **Provisioning, hosting, upgrading, or repairing RabbitMQ brokers** — the adopter supplies the broker
- **Operating a hosted SaaS for third parties** — this is self-hosted software the adopter runs on their own infrastructure; multi-tenancy means one install serving several teams inside one organisation, not a public service
- **Creating or altering dead-letter topology** — including adding `x-dead-letter-exchange` to an existing queue
- **Azure Service Bus / AMQP 1.0 support** — different protocol, different product
- Alerting / PagerDuty integration
- Message flow topology graph (nice, but expensive to render correctly)
- Kafka / SQS adapters
- Historical analytics beyond retention window

---

## 12. Naming and identifiers

Fixed now so that the C#, Go, and Rust implementations agree, and so nothing has to be renamed once code exists.

| Artifact | Value |
|---|---|
| Product name | Marites |
| Repository / folder | `marites` |
| .NET namespace / root | `Marites` |
| Binary / CLI | `marites` |
| Container image | `marites` |
| Helm chart / release | `marites` |
| Environment variable prefix | `MARITES_` |
| Shadow queue prefix | `marites.{tenant}.{hash}` |
| Custom header prefix | `x-marites-` |
| Default Postgres database | `marites` |
| Roles (Entra App Roles) | `Admin`, `Support`, `Viewer` |
| Default HTTP port | `8080` |

**Why the prefixes matter:** the shadow queue prefix is what the broker ACL grants `configure`/`write` on. If it changes after deployment, every existing capture binding becomes orphaned and every adopter has to update their ACLs. It is effectively permanent once shipped — which is why it is pinned here rather than decided later.
