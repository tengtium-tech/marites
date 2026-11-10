---
document_id: DOC-OPS-001
title: Deployment and Onboarding Specification
product: Marites
version: 0.1
date: 2026-11-07
status: Draft — all decisions resolved; no implementation
---

# DOC-OPS-001 — Deployment and Onboarding Specification

**Source:** [requirements](../business/requirements.md) FR-012…FR-014, [architecture](../architecture/architecture-overview.md). Owning document for adopter obligations, deployment, and readiness.

## 1. Prerequisites — the adopter brings the broker

**Marites is a client. It never provisions, hosts, upgrades, or repairs a RabbitMQ broker.** An adopter must already have a running, correctly configured broker *before* the application does anything useful. This section is a hard prerequisite; a misconfigured broker produces an application that looks broken.

### 1.1 A running RabbitMQ broker

| Source | Notes |
|---|---|
| Self-hosted | 3.12+ or 4.x, AMQP 0-9-1 |
| Docker / Compose | Fine for evaluation; the management plugin must still be enabled |
| CloudAMQP, Amazon MQ, similar managed | Management API availability varies — see 1.2 |
| Azure VM / AKS container | Works; Azure offers no managed RabbitMQ, the adopter runs it |
| Kubernetes via the RabbitMQ Cluster Operator | Recommended for the K8s profile |

> **Terminology — resolved.** "Service Bus" was ambiguous when this document was first written. DEC-001 settled it: three platforms are supported at `full` tier — RabbitMQ, Azure Service Bus and Google Pub/Sub. §1.1 to §1.4 cover RabbitMQ only; the other two providers' prerequisites are in §1.6. *"A message broker running as a managed service"* means any hosting of RabbitMQ in the table above qualifies.

### 1.2 Required broker configuration

All of the following are required for full functionality.

- [ ] **Management plugin enabled** — `rabbitmq-plugins enable rabbitmq_management`. All metadata comes from `/api/queues`, `/api/exchanges`, `/api/bindings`. Without it there is no dashboard, only captured payloads.
- [ ] **AMQP port reachable** — 5672, or 5671 with TLS.
- [ ] **Management port reachable** — 15672, ideally behind HTTPS, and reachable *from the application*, not merely from a browser.
- [ ] **A vhost** — a dedicated vhost per environment, rather than sharing `default`.
- [ ] **A dedicated least-privilege user**, never `guest`, never administrator:
  - `monitoring` tag — read connections, channels, queues
  - `configure`, `write` and `read` on `^marites\.` — **mandatory** (DEC-003). `configure` and `write` let Marites declare and bind its own shadow queues; `read` lets it consume them. Without it there is no continuous capture, and the product cannot function as designed.
  - `write` on watched exchanges — **mandatory**: binding a queue to an exchange is a write against the exchange, so capture cannot start without it
  - `read` on watched queues — needed to peek, and to consume the shadow queue when it shares the watched queue's vhost
  - `write` on the exchanges replay and publish target — the same watched exchanges when replay is enabled, which is why this grant is often already present
- [ ] **TLS** on AMQP and management for anything outside localhost
- [ ] **`rabbitmq_tracing` plugin** — only for the Strategy C fallback; off by default

**Managed-broker caveats.** Amazon MQ restricts parts of the management API and blocks some topology operations, which can disable continuous capture entirely. CloudAMQP permits it with per-plan limits. Verify against the provider before committing to shadow bindings (DEC-003).

### 1.3 Dead-letter topology must already exist

**The application observes dead lettering; it does not create it.** For dead-letter monitoring to show anything, the adopter must already have configured:

- [ ] `x-dead-letter-exchange` on each source queue
- [ ] A DLQ declared and bound to that dead-letter exchange
- [ ] `x-dead-letter-routing-key` where the DLX is not a fanout
- [ ] A consumer policy that actually rejects or nacks — otherwise nothing dead-letters

If a queue has no DLX, the application **detects and warns** but will not fix it. Changing queue arguments requires deleting and redeclaring the queue, destroying messages — never done automatically (DEC-004).

### 1.4 Network and resource budget

- **Network:** application → broker on AMQP and management ports, both directions for acks. In Kubernetes this means egress rules plus any broker-namespace network policy.
- **Broker disk:** each shadow queue is bounded by `x-max-length × average message size`. Reserve that per watched queue.
- **Application disk:** `retention × daily volume × average size`, plus spillover for payloads over 64 KB.

### 1.5 Capture prerequisites and cost by provider

"Save every message" requires **non-destructive duplication** — fan-out or a replayable log. Without it there is no way to observe traffic except by consuming, which competes with the real consumer.

| Requirement | `rabbitmq` | `azure_service_bus` | `google_pubsub` |
|---|---|---|---|
| Non-destructive duplication | binding | subscription | subscription |
| Native dead-letter with reason | ✓ | ✓ | ✓ |
| Peek without consuming | get + requeue | `PeekMessages` | pull and hold |
| **Adopter cost of a capture stream** | **disk only** | **≈2× delivery operations** | **≈2× delivery** |

**The cost row is not a footnote.** On Service Bus and Pub/Sub, a monitoring subscription means every message is delivered twice and billed twice. On RabbitMQ a shadow queue costs storage, not billable operations. An adopter evaluating Marites on a high-volume Service Bus namespace should size that multiplier before enabling capture on a firehose topic — `payload_policy = none` reduces storage, not delivery operations.

**Platforms excluded, and why.** Apache Kafka, Amazon SQS and Amazon SNS were removed from scope (DEC-001, 07 November 2026). Kafka has no native dead lettering — a DLQ is a naming convention, so the product's core metric would become an inference rather than an observation. SQS and SNS have no fan-out and no log, so there is no non-destructive way to observe them at all; making them observable would require the adopter to change their own topology. A provider list where half the entries cannot deliver the product's central promise is worse than a shorter list where all of them can.

### 1.6 Prerequisites for the other providers

Drafted here until 09 November 2026; now owned by [provider prerequisites](provider-prerequisites.md), which carries the identities, the exact rights, the commands that verify them and the preflight mapping for Azure Service Bus and Google Pub/Sub.

Two facts belong here rather than there, because they are about the shape of the obligation rather than about a platform:

- **Marites needs the right to create and delete one subscription per watched topic**, and to receive from it. There is no equivalent of the RabbitMQ prefix grant: Azure and Google scope by role assignment, so the grant is scoped by resource rather than by name.
- **Bare queues are not tappable on either platform.** A queue with no fan-out cannot be observed without competing with production, so only topics are watched ([VOCABULARY §12](../reference/vocabulary.md)).

Preflight (§2) runs the same six checks per provider: management reachable, topology writable, dead-letter configured, clock skew, and the two identity checks.

### 1.7 Payload responsibility — what you publish is what we store

**Marites stores message bodies exactly as published.** There is no masking, redaction, or field filtering. If a payload contains a credential, token or personal data, that value is stored.

**Redaction is the producer's responsibility.** If a value must not be retained, remove or replace it before publishing. This is the same contract every log and observability platform offers: they record what they are sent, and sanitising is the sender's job.

Three things an adopter must understand before enabling capture:

- **`payload_policy = none` is the only way to avoid storing bodies.** Counting is unaffected — `published` and `dead_lettered` keep incrementing, so the reporting dashboard still works.
- **Volume encryption protects data at rest**, against a stolen disk or backup. It does not protect against anyone with database access.
- **Content hashes are HMAC-keyed**, so they cannot be used to confirm a guessed value.

**Where an adopter's policy requires redaction inside the tool, Marites is not the right product for that queue.** Stating this up front costs a conversation; discovering it after go-live costs a compliance incident.

## 2. Preflight readiness check (FR-013)

Documentation drifts; a checklist that runs does not. `POST /api/v1/brokers/{id}/preflight` returns pass/fail per item:

| Check | Failure means |
|---|---|
| Management API reachable and authenticated | No dashboard at all |
| Vhost visible to the credentials | Wrong user or vhost |
| `configure`/`write`/`read` on `marites.` prefix, and `write` on each watched exchange (declare, bind, attach a consumer to, and delete a probe queue) | Capture impossible — Strategy B/C only |
| Each selected queue reports a DLX | Dead-letter monitoring will show nothing |
| Shadow queue declarable with bounded args | Broker policy blocks topology writes |
| Clock skew application ↔ broker < 5 min | `x-death` timestamps will be wrong |

The React setup wizard runs this inline. A failure on reachability or credentials blocks saving. A failure on topology permissions saves the profile as `failing`, refuses to enable capture (`ERR-BRK-001`) and names the missing grant. Adopters get a precise error instead of a dashboard full of zeros.

## 3. Deploying the application

Once §1 is satisfied, the application deploys on **the adopter's own server** as a single self-contained service — no broker sidecar, no agent on the RabbitMQ host, no privileged access beyond the credentials in §1.2.

| Shape | How | Suited to |
|---|---|---|
| Docker Compose | `docker compose up -d` with `.env` | Single server, evaluation |
| Container | `marites` image + Helm chart or plain manifest | Production, multi-broker |
| Self-contained binary | `dotnet publish -r <rid> --self-contained` | Air-gapped environments |

**Network direction:** the application needs **outbound** access to the broker. It needs **no inbound** access to the broker, and the broker needs none to the application. Only the application's HTTP port is exposed to users — nothing new becomes reachable from the broker's network.

### 3.1 The image

| Property | Value |
|---|---|
| Base | `mcr.microsoft.com/dotnet/aspnet`, non-root user, read-only root filesystem |
| Entrypoints | One image, three commands: `api`, `worker`, `all` (ADR-009) |
| Ports | `8080` for users; `9090` for health and metrics, **not published** to the user network |
| Writable paths | The bookkeeping store's directory, and nothing else |
| Version stamp | The image reports its version and the schema version it expects at startup |

**One image, three entrypoints, is what keeps "single container" honest.** An adopter who runs `all` gets the whole product in one process; one who separates `api` from `worker` gets the same binary with a different command, not a second build.

### 3.2 Health, readiness and metrics

| Endpoint | Port | Answers |
|---|---|---|
| `/health/live` | 9090 | Is the process running. Never touches the database or the broker |
| `/health/ready` | 9090 | Can it serve: bookkeeping store open, message store reachable **if configured** |
| `/metrics` | 9090 | Prometheus exposition: capture rate, lag, queue depth cache age, operation counts |

**Readiness does not fail when the message store is down.** The product degrades to live-only and keeps working (DEC-016); a readiness probe that pulled the container out of service would turn a history outage into an outage. The message store's health is a **gauge**, not a gate.

**`/health/live` never touches a dependency.** A liveness probe that fails on a slow broker restarts a healthy process, and the restart makes the incident worse.

### 3.3 TLS and the reverse proxy

**Marites does not terminate TLS.** It expects to sit behind the adopter's proxy, which owns certificates, ciphers and renewal — the same reasoning that keeps it out of the broker's topology.

| Requirement | Why |
|---|---|
| Forward the original host | `MARITES_URL` and the OAuth redirect must match what the browser sees |
| Pass `X-Forwarded-For` | The audit records the caller's address; behind a stripping proxy it records null, which the dictionary already allows |
| Do not buffer `/api` | A `202` with a long-running operation must reach the client |
| WebSocket upgrade for the drawer | The chat stream is request/response, but the connection is upgraded |
| TLS 1.2 minimum, 1.3 preferred | The token in the `Authorization` header is the only credential in flight |

**Plain HTTP is refused outside loopback**, at startup, with the same guard that refuses `MARITES_AUTH_MODE=none` on a public address. A self-hosted product that silently serves tokens over cleartext is a self-hosted product that gets flagged in its first security review.

### 3.4 Logs

| Property | Value |
|---|---|
| Format | JSON, one object per line, to stdout |
| Contents | Timestamp, level, operation id, queue, actor id — **never a message body**, at any level |
| Correlation | The `Idempotency-Key` and the audit row id appear on every line for an operation, so a log and the audit can be joined |
| `debug` | Adds headers and payload **sizes**, still never bodies |

**The logs and the audit are different records and must not converge.** The audit is permanent and answers *who did what*; the logs are rotated and answer *why it was slow*. A body in the logs would be a copy of the evidence with none of its retention rules.

## 4. Pointing at a broker (FR-012)

The application ships with **zero broker configuration baked in**. Connection details are runtime data — a **broker profile**:

| Field | Example | Notes |
|---|---|---|
| AMQP URI | `amqps://marites:••••@rabbit.internal:5671/prod` | 5671 + TLS for anything non-local |
| Management URI | `https://rabbit.internal:15672` | Required for metadata; may be a different host |
| vhost | `/prod` | Must already exist; the application does not create vhosts |
| Credentials | the least-privilege user from §1.2 | Never `guest`, never administrator |
| TLS options | CA bundle, hostname verification | Self-signed internal CAs are common; verification stays on by default |

Two entry paths:

1. **UI setup wizard** (recommended) — runs preflight inline, refuses to save a failing profile.
2. **Config file / environment variables** (prefix `MARITES_`) — for GitOps. Secrets come from the environment or a secret manager and are never committed.

**Multiple brokers are first-class** (FR-014). One instance can watch a dev broker and a production broker side by side; the dashboard groups by profile, and cluster identity is always `(broker_profile_id, vhost, queue_name)`.

**Secret handling:** credentials encrypted at rest (AES-GCM, key from env/KMS), never returned by the API, never written to logs, masked in the UI. AMQP URIs pasted with embedded passwords are redacted on read (NFR-003).

### 4.1 What Marites creates in the adopter's broker

Adopters run their own platform; Marites only adds an observation point to it. Being precise about what appears is a security-review requirement, not a courtesy.

| Provider | Creates | Never touches |
|---|---|---|
| `rabbitmq` | One queue `marites.{tenant}.{hash}` and one binding, per watched exchange | Their queues, exchanges, policies, consumers, DLX configuration |
| `azure_service_bus` | One subscription per watched topic | Their queues, topics, existing subscriptions, rules |
| `google_pubsub` | One subscription per watched topic | Their topics, existing subscriptions, retention settings |

**Properties of everything created:**

- Named with the `marites.` prefix, so it is identifiable and the ACL can be scoped to it
- Bounded by `x-max-length` and `x-message-ttl` (RabbitMQ) or an explicit ack deadline (Service Bus, Pub/Sub) — it can never grow without limit
- Removable on demand; removing a broker profile or disabling capture cleans up its own objects and leaves the adopter's topology untouched
- Never a modification of an existing object — Marites does not alter queue arguments, exchange types, or consumer policies

## 5. End-to-end first run

```
Deploy  →  Open UI  →  Add broker profile  →  Preflight  →  Pick queues
                                                              ↓
                            Dashboard live  ←  Capture bound + backfill
```

1. Deploy the container or binary; expose the HTTP port.
2. Open the UI — first run shows the setup wizard, not an empty dashboard.
3. Enter the broker profile. Preflight runs immediately; fix anything red.
4. Select queues and exchanges to watch. Queues already reporting a DLX are pre-selected.
5. Confirm — capture bindings are declared and a bounded backfill is offered for existing backlog.
6. Dashboard goes live within one poll interval (~10 s).
7. Repeat 3–5 per additional broker; add users and roles when moving past the single-user profile.

**Time to first value: minutes**, assuming §1.2 is satisfied. If preflight fails on topology permissions, capture cannot be enabled: DEC-003 makes that a hard failure with a named remedy. The profile remains usable for the live console (queue metadata and the M1 operations) and the UI says plainly why capture is off — never a silent downgrade.

## 6. Deployment profiles and milestones

Same `marites` binary in every profile; the difference is authentication and scale, never the storage engine.

| Profile | Store | Auth | Isolation |
|---|---|---|---|
| Local / single broker | PostgreSQL (Compose) | `MARITES_AUTH_MODE=none`, loopback only | single implicit tenant |
| Multi-broker | PostgreSQL | Entra ID + JWT | tenant column, capability policies |
| K8s / multi-team | PostgreSQL (managed) | Entra ID, one app registration (ADR-014: one install, several teams) | row-level tenant policy, per-tenant keys |

**SQLite is deliberately unsupported** (ADR-004). The data layer depends on binary `COPY`, native partitioning, `JSONB`, and GIN indexes — all Postgres-only. A second provider would mean two code paths for the most performance-critical component, and the "lightweight" profile would silently lose partitioning, which is what keeps retention from becoming a DELETE storm.

Ships as a Docker image plus Helm chart. Capture workers scale by sharding `queue_ref` across replicas, each queue owned by exactly one worker via PostgreSQL advisory lock.

### Milestones

| # | Deliverable | Exit criteria |
|---|---|---|
| M0 | Contract + skeleton + preflight + deploy | `openapi.yaml` reviewed; `/capabilities` and `/brokers/{id}/preflight` served; setup wizard blocks save on failure; `docker compose up` yields a working UI against a test broker; CI green |
| M1 | Metadata dashboard + **live support console** | Queue tree, depths, rates, DLQ badges; live peek, discard-one, purge and redrive against the broker; audit log writing; RBAC enforced. **A Support user can complete a recovery without leaving the UI** |
| M2 | Capture pipeline | Shadow binding + backfill; rollups written every second; rows land in store; retention sweeper runs |
| M3 | Message explorer + historical actions | Search and filters, payload viewer, duplicate grouping; replay a captured message; publish |
| M4 | Multi-broker + tenancy | Bulkhead isolation across clusters, environment-scoped confirmations, per-tenant isolation tests (broker CRUD and encrypted credentials ship in M0) |
| M5 | Provider #2 — Azure Service Bus | Adapter passing the shared conformance suite at `full` tier |
| M6 | Provider #3 — Google Pub/Sub | Adapter passing the shared conformance suite at `full` tier |

**Why live operations moved from M4 to M1.** Support is the primary actor (DOC-SRS-001 §2), and steps Detect and Act do not depend on the capture pipeline — they act on the live queue through the broker API. Holding them until M4 would mean three milestones in which the primary user can look but not act.

**The cost is real:** M1 now includes the audit log and RBAC, because shipping destructive operations without them is unacceptable. That is scope pulled forward, not scope added — both were required anyway, and both are cheaper to build alongside the first destructive operation than to retrofit.

**M2's value is unchanged.** If broker topology permissions turn out to be blocked (DEC-003), M1's live console still works via peek and management-API operations, and the product remains useful rather than stalled.

## 7. Document control

Revision 0.1 records adopter obligations and deployment shape. DEC-003 (topology permissions) and DEC-004 (existing DLX topology) change §1.2, §1.3, and §2. No deployment has been performed and no preflight has been executed.

*Amended 09 November 2026:* §1.6 reduced to the shape of the obligation, with the identities, rights and verification commands moved to [provider prerequisites](provider-prerequisites.md). §1.2 corrected: binding a queue to an exchange is a write against the exchange, so `write` on watched exchanges is mandatory rather than `read`, and the publish grant is usually the same grant. Recorded as AUD-035.
