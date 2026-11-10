---
document_id: VOCABULARY
title: Canonical Vocabulary and Enumerations
product: Marites
version: 0.1
date: 2026-11
status: Controlled — single source for enumerated values
---

# Canonical Vocabulary and Enumerations

**Every enumerated value in this system is defined here exactly once.** Other documents cite this file; they do not restate it.

## Recovery, and why the word is qualified

Two different things in this product are called recovery, and confusing them makes a storage decision look like a safety decision.

| Term | Means | Document |
|---|---|---|
| **Recovery** | Acting on the traffic: replay a dead letter, discard a poison message, purge a queue, publish a correction. The *Act* step of Detect → Understand → Act → Prove | [Recovery lifecycle](../03-workflows/recovery-lifecycle.md) |
| **Restore** | Returning Marites' own store to an earlier state after loss — a backup, a restore, a rebuilt node | [Backup and recovery](../11-operations/backup-and-recovery.md) |

Recovery needs no message store: every operation writes its audit row to Marites' own store, which always exists (DEC-012, DEC-016). Restore is about that store. Where a sentence could be read either way, write **recovery** or **restore**, never "recovery" alone.

---

## Why this file exists

Marites' enumerated values are consumed by four separate artefacts — the C# implementation, the OpenAPI contract, the database schema, and the React UI. Each is written at a different time and none reads the others' source. That is the exact condition under which enums get re-derived plausibly and incorrectly: `role` gains a fourth value, `death_reason` gets spelled `dead_lettered` in one place and `rejected` in another, and a metric name ends up meaning two things.

Prose cannot prevent this. A single shared definition can.

**Rule:** if a document, a migration, an API schema, or a UI label needs an enumerated value, it links here. Restating the values inline is a defect — the second copy drifts, and the copy that drifts is always the one someone reads.

**Where to look:** roles and capabilities §2 · message metrics §3 · dead-letter reasons §4 · payload policy §5 · replay state §6 · preflight state §7 · environment tags §8 · identifiers §9 · queue identity §10 · terms §11 · **provider kinds §12** · credential kinds §13 · operation support §14 · capture state §15 · capture mode §16 · AI provider §17.

---

## 1. Modules

The twelve .NET source projects. Fixed by ADR-005; structure owned by [DOC-ARCH-001 §6](../05-architecture/architecture-overview.md).

| ID | Project | Responsibility |
|---|---|---|
| `Contracts` | `Marites.Contracts` | OpenAPI-generated DTOs, enums, error codes. Zero dependencies. |
| `Domain` | `Marites.Domain` | The six domain types. Behaviour only where it prevents a bug. |
| `Brokers` | `Marites.Brokers` | The four ports (`IBrokerAdmin`, `IMessageTap`, `IMessagePublisher`, `IMessageInspector`), the provider registry and the capability model. References no provider SDK. |
| `Brokers.RabbitMq` | `Marites.Brokers.RabbitMq` | AMQP 0-9-1 and Management API adapter — the reference implementation. |
| `Brokers.ServiceBus` | `Marites.Brokers.ServiceBus` | AMQP 1.0 adapter over `Azure.Messaging.ServiceBus`. |
| `Brokers.PubSub` | `Marites.Brokers.PubSub` | gRPC adapter over `Google.Cloud.PubSub.V1`. |
| `Storage` | `Marites.Storage` | DbContext, migrations, COPY bulk writer, partition sweeper. |
| `Operations` | `Marites.Operations` | Guard chain, dry-run, rate limiting, audit. |
| `Capture` | `Marites.Capture` | Topology setup, consume loop, batching, backpressure. |
| `Identity` | `Marites.Identity` | Entra ID validation, role→capability mapping, policies. |
| `Api` | `Marites.Api` | HTTP endpoints, one folder per slice. Composition root. |
| `Worker` | `Marites.Worker` | Capture host entrypoint. |

---

## 2. Roles and capabilities

Entra App Roles. Exactly three; there is no fourth and no "super user" beyond Admin plus break-glass.

| Role | Claim value | Meaning |
|---|---|---|
| Viewer | `Viewer` | Reads the reporting dashboard and queue metadata. No message list, no payloads, no broker mutation (DEC-002). |
| Support | `Support` | Viewer plus message list and payloads, and operational recovery: peek, discard, replay, publish. |
| Admin | `Admin` | Everything Support can do, plus purge, broker profiles, capture rules, role assignment. |

Capability policy names, used in code and in the API contract:

`CanViewQueues`, `CanViewPayloads`, `CanPeek`, `CanDiscard`, `CanReplay`, `CanPublish`, `CanPurge`, `CanManageBrokers`, `CanManageCaptureRules`, `CanAssignRoles`, `CanViewAuditLog`, `CanPropose`, `CanExecute`

Grants per role are owned by [DOC-UX-001 §2](../08-experience/dashboard-and-operations.md). This file owns the names, not the matrix.

`CanPropose` is the one capability **no human role holds**. It belongs to service principals — an agent may create a proposal and nothing else ([ADR-020](../05-architecture/decisions.md)). Granting it to a person would give them a slower path to an operation they can already perform.

**Break-glass** is a flag on `app_user` (`is_break_glass`), not a role. It is disabled by default, enabled by environment variable, and every use is audited.

---

## 3. Message metrics

These are **separate series**. Summing them is a defect (see [DOC-DATA-001 §3.2](../07-data-and-contracts/data-model.md)).

| Name | Definition | Unit |
|---|---|---|
| `published` | Messages routed into a watched exchange | count |
| `dead_lettered` | Messages arriving at a DLX | count |
| `dead_letter_rate` | `dead_lettered / published` over the same window | ratio |
| `peak_rate` | Highest observed messages/second in the window | msg/s |
| `bytes` | Sum of message body sizes observed | bytes |
| `messages_seen` | Total capture events across all sources | count — **pipeline health only, never a business metric** |

**`granularity`:** `minute` · `hour` · `day` — the three storage tiers of `message_rollup` ([DOC-DATA-001 §3.1.2](../07-data-and-contracts/data-model.md)). Query-time periods `week`, `month`, `quarter` and `year` are summed from `day` rows and are never stored, so they are not values of this enum.

---

## 4. Dead-letter reasons

Values mirror RabbitMQ's `x-death` header. Do not invent additional values; if RabbitMQ adds one, add it here first.

| Value | Cause |
|---|---|
| `rejected` | Consumer nacked or rejected without requeue |
| `expired` | Message or queue TTL elapsed |
| `maxlen` | Queue length limit exceeded |
| `delivery_limit` | Delivery attempt limit reached |
| `convention` | Reserved — no v1 provider. Death inferred from a naming convention where the platform exposes no dead-letter metadata |
| `unknown` | The platform reported a dead letter without exposing a reason |

The provider's own wording is preserved separately in `native_reason` — see [DOC-DATA-001 §2](../07-data-and-contracts/data-model.md). Normalising to these six values must never discard the original string.

---

## 5. Payload policy

Governs payload retention only. **Counting is never affected by payload policy** — a queue set to `none` still reports exact counts.

| Value | Stored |
|---|---|
| `full` | Headers plus complete payload. Suggested, never applied silently, for low-volume queues such as DLQs (DEC-005). |
| `sampled` | Headers for all; payload for 1 in N. **Shipped default, 1 in 100** (DEC-005). |
| `none` | Headers only — routing key, size, death reason, timestamps. The only way to avoid storing bodies. |

---

## 6. Replay job state

| Value | Meaning |
|---|---|
| `pending` | Created, dry-run passed, awaiting execution |
| `running` | Publishing, within rate limit |
| `halted` | Auto-paused because the target queue grew faster than it drained; requires human resume |
| `completed` | All selected messages published |
| `failed` | Terminated by error; `halt_reason` populated |

---

## 7. Preflight state

| Value | Meaning |
|---|---|
| `unknown` | Never run |
| `passing` | All checks passed |
| `failing` | At least one check failed; the failing check names are returned |
| `degraded` | Metadata reachable but topology writes blocked — capture cannot be enabled (DEC-003); the live console remains available and states why |

---

## 8. Environment tag

Free-form but conventionally one of `prod`, `staging`, `dev`, `test`. `prod` is special-cased in the UI: it is visually distinct and requires the typed environment tag for replay, publish and purge.

---

## 9. Identity and naming

Fixed now so the contract, the implementation and the UI agree, and so nothing must be renamed after deployment.

| Artifact | Value |
|---|---|
| Product name | Marites |
| Repository / folder | `marites` |
| .NET namespace root | `Marites` |
| Binary / CLI | `marites` |
| Container image | `marites` |
| Helm chart / release | `marites` |
| Environment variable prefix | `MARITES_` |
| Shadow queue prefix | `marites.{tenant}.{hash}` |
| Custom header prefix | `x-marites-` |
| Default PostgreSQL database | `marites` |
| Default HTTP port | `8080` |
| Entrypoints | `marites api` · `marites worker` · `marites all` |

**The shadow queue prefix is effectively permanent once shipped.** It is what the broker ACL grants `configure`/`write`/`read` on. Change it after any deployment and every capture binding is orphaned and every adopter must edit their RabbitMQ permissions.

---

## 10. Queue identity

A queue is **never** identified by name alone. `orders` exists in prod and staging simultaneously.

```
(broker_profile_id, vhost, queue_name)
```

Every captured row carries `broker_profile_id`. Any key, cache entry, index, or UI route that uses queue name alone is a defect.

---

## 11. Terms

| Term | Meaning |
|---|---|
| **Shadow queue** | A Marites-owned queue bound to an adopter exchange, receiving a copy of each routed message. The tap mechanism. |
| **Tap** | The act of observing traffic without consuming it from the production path. |
| **Relay** | An optional single-process bridge for network-isolated deployments. Not a broker, not a cluster. |
| **Backfill** | One-shot retrieval of messages already sitting in a queue, via Management API get+requeue. |
| **Adopter** | The organisation installing Marites and supplying the broker. Not a UI user. |
| **Dead letter** | A message that reached a dead-letter exchange after rejection, expiry, overflow, or delivery-limit exhaustion. |

---

## 12. Provider kinds and capabilities

An adopter selects a platform when registering a broker profile. `provider_kind` is a closed enum — adding a provider means adding a value here first, then an adapter, then a migration.

| `provider_kind` | `support_tier` | Platforms covered | `addressing_model` | `dead_letter_model` | `counting_model` | Tap |
|---|---|---|---|---|---|---|
| `rabbitmq` | `full` | RabbitMQ, Amazon MQ for RabbitMQ, CloudAMQP, K8s Operator | `exchange_routing` | `dlx` | `exact` | yes |
| `azure_service_bus` | `full` | Azure Service Bus | `topic_subscription` | `dlq_subqueue` | `exact` | topics yes, bare queues no |
| `google_pubsub` | `full` | Google Cloud Pub/Sub | `topic_subscription` | `dead_letter_topic` | `exact` | yes |

**Three providers, all `full`.** Kafka, Amazon SQS and Amazon SNS were removed from scope in November 2026 (DEC-001) because they cannot satisfy the product's core promise — see DOC-OPS-001 §1.5. Kafka has no native dead lettering, and SQS and SNS have no non-destructive way to observe traffic at all. Shipping them with caveats would have made half the provider list second-class.

### Support tiers

`support_tier` states what an adopter can actually expect, so a listed provider never overstates what it delivers.

| Tier | Meaning | Guarantees |
|---|---|---|
| `full` | Complete implementation | Continuous non-destructive capture, exact counts, observed dead-letter reason, replay and publish where the platform permits |
| `observational` | Capture works, semantics are inferred | Continuous capture, but dead-letter status is inferred from a naming convention and counts are derived. UI must label both. |
| `metadata_only` | No continuous capture | Topology and queue metadata, point-in-time peek where available, no volume history. The UI must state that capture is unavailable, not display zeroes. |

**v1 uses `full` only.** The other two tiers remain defined because they are the honest way to add a platform later that cannot meet every guarantee — `observational` for a log-based system with no dead-letter concept, `metadata_only` for a queue-only system with no fan-out. Removing them would leave no vocabulary for a future provider except overstating it.

Closed enums used inside `ProviderCapabilities` ([DOC-ARCH-001 §6.2](../05-architecture/architecture-overview.md)):

**`support_tier`:** `full` · `observational` · `metadata_only`

**Boolean capabilities:** `can_tap`, `can_peek`, `can_publish`, `can_discard`, `can_purge`, `can_replay`, `can_declare_shadow`

**`replay_model`:** `republish` · `offset_reset` · `seek` · `redrive` · `none`

**`discard_model`:** `delete_one` · `truncate` · `purge_entity` · `none`

**`addressing_model`:** `exchange_routing` · `topic_subscription` · `queue_only` · `log_offset`

**`dead_letter_model`:** `dlx` · `dlq_subqueue` · `redrive_policy` · `dead_letter_topic` · `convention` · `none`

**`counting_model`:** `exact` · `approximate` · `derived`

`counting_model` is a correctness field. Where it is `approximate` or `derived`, the UI must display `≈`. Presenting an estimate as an exact count is a defect, not a formatting preference.

---

## 13. Credential kinds

Each provider authenticates differently. A username/password column cannot represent a SAS key, a service-account JSON, or an assumed IAM role, so `credential_kind` selects the shape of the encrypted credential payload.

| `credential_kind` | Provider | Encrypted fields | Stores a secret? |
|---|---|---|---|
| `basic` | `rabbitmq` (AMQP) | username, password | Yes |
| `http_basic` | `rabbitmq` (management API) | username, password | Yes |
| `mtls` | `rabbitmq` | client cert PEM, client key PEM, optional CA | Yes |
| `sas_key` | `azure_service_bus` | policy name, key | Yes |
| `entra_service_principal` | `azure_service_bus` | tenant id, client id, client secret | Yes |
| `service_account_json` | `google_pubsub` | the service-account JSON document | Yes |
| `managed_identity` | `azure_service_bus`, `google_pubsub` | none | **No** |
| `workload_identity` | `google_pubsub` | none | **No** |
| `aws_iam_key` | *reserved* — no v1 provider | access key id, secret access key, optional session token | Yes |
| `aws_iam_role` | *reserved* — no v1 provider | role ARN, optional external id, base credential | Yes (base) |
| `sasl_plain` | *reserved* — no v1 provider | username, password | Yes |
| `sasl_scram` | *reserved* — no v1 provider | mechanism, username, password | Yes |

Reserved kinds are retained for the same reason as the reserved replay models: adding a platform later should not require inventing credential vocabulary under delivery pressure.

**Ambient kinds are preferred.** `managed_identity` and `workload_identity` store no secret at all — the platform supplies the credential at runtime. Where an adopter's environment supports them, the UI should present them first, because the most secure option should not also be the hardest one to configure.

**RabbitMQ needs two credential slots.** The AMQP connection and the management API are separate authentication surfaces and may use different users. The encrypted payload is therefore a JSON object with named slots — `amqp` and `admin` — not a flat pair. Other providers populate a single slot.

**Credentials are write-only.** No endpoint returns them in any form, including partially masked; the UI displays only `credential_kind` and a last-updated timestamp. Rotation is an admin action that replaces the encrypted payload without redeploying the service.

---

## 14. Operation support

Not every provider supports every operation, and where it does the mechanism differs. `can_publish` alone is too coarse — "replay" means two different things across the three providers.

| Operation | `rabbitmq` | `azure_service_bus` | `google_pubsub` |
|---|---|---|---|
| Publish | ✓ | ✓ | ✓ |
| Peek | ✓ get + requeue | ✓ `PeekMessages` | ~ pull without ack |
| Discard one | ✓ ack | ✓ complete | ✓ ack |
| Purge | ✓ purge queue | ✓ delete entity | ~ `Seek` past backlog |
| Replay | ✓ republish | ✓ republish | ✓ `Seek` |

Legend: ✓ supported · ~ supported with different semantics · ✗ unavailable.

### Replay is not one operation

| `replay_model` | Providers | What it actually does |
|---|---|---|
| `republish` | `rabbitmq`, `azure_service_bus` | Re-publish the selected messages to their original destination |
| `seek` | `google_pubsub` | Rewind a subscription to a timestamp or snapshot |
| `redrive` | *reserved* — no v1 provider | Native move of dead-letter messages back to the source |
| `offset_reset` | *reserved* — no v1 provider | Rewind a consumer group to an offset |
| `none` | *reserved* — no v1 provider | Not available |

`redrive` and `offset_reset` are retained as reserved values rather than deleted. Both describe real mechanisms on platforms outside v1 scope, and re-adding a provider should not require inventing vocabulary under delivery pressure — which is precisely when a new value gets defined inconsistently.

**If `offset_reset` is ever used, it carries different risk from the others.** It rewinds an entire consumer group rather than a message, so it reprocesses everything from that point; it typically requires the group to be inactive; and it cannot be rate-limited per message. It would have to be a distinct, Admin-only operation with an explicit warning — never the same "Replay" button with a different backend.

### Discard is not one operation

| `discard_model` | Providers | What it actually does |
|---|---|---|
| `delete_one` | `rabbitmq`, `azure_service_bus`, `google_pubsub` | Remove a single message by handle or ack |
| `purge_entity` | `rabbitmq`, `azure_service_bus` | Remove all messages in a queue or entity |
| `truncate` | *reserved* — no v1 provider | Delete records before a position |
| `none` | *reserved* — no v1 provider | Not available |

Where an operation is unavailable, the UI **omits the control** rather than disabling it with no explanation, and the capability response is the reason. A disabled button with no stated cause is a support ticket; an absent button plus a stated tier is a product decision.

---

## 15. Capture state

`QueueSummary.captureState` in the contract. Five values, and every one of them exists to stop a number being misread — which is why the client must render the state, never infer it from a count of zero.

| Value | Meaning | What the client must show |
|---|---|---|
| `live` | Capture is bound and advancing | Normal figures |
| `stale` | Capture has stopped advancing while depth grows, or the writer has stalled | Stale badge with `lastSeenAt`; never a zero count |
| `capped` | The storage ceiling was reached; rows paused and counting continues | "Capped" with an explanation that counts are still accruing |
| `disabled` | Capture is not enabled for this queue | Nothing captured yet; offer to enable it |
| `unavailable` | Capture cannot be enabled — topology write refused, or the queue is gone | The failing check or grant, by name (`ERR-BRK-001`) |

`stale` is the dangerous value: it is the only one that describes a system that looks quiet and is not. `disabled` and `unavailable` differ in who can fix them — the first is a choice, the second is a permission.

## 16. Capture mode

`capture_rule.capture_mode` and `CapturePolicy.captureMode`. How much of a message is kept — a different question from `payload_policy` (§5), which decides whether the body is kept *within* a row.

| Value | Stored per message | Counted | Inspection |
|---|---|---|---|
| `rows` | One row: routing key, headers, size, death reason, payload per §5 | ✓ | ✓ |
| **`counts`** | **Nothing.** The rollup counter increments; no row is written | ✓ | ✗ for that queue |

**`counts` is the answer to "we cannot let you store our messages."** `payload_policy = none` keeps a row without the body, which a compliance review can still read as storing the message. `counts` stores nothing but a number (DEC-013).

**What it costs.** Message search, payload inspection and captured-message replay are unavailable on that queue. Live peek, discard, purge and redrive still work, because they act on the broker rather than on the store. The dashboard and the executive trend are unaffected — they read rollups.

## 17. AI provider

`ai_provider.kind`. The assistant runs on a model the adopter supplies (DEC-015). Four kinds, and the one that stores no secret is recommended.

| `kind` | Stores | Note |
|---|---|---|
| `local` | base URL, model — **no secret** | Ollama or vLLM. Nothing leaves the network. **Recommended where allowed** |
| `azure_openai` | endpoint, deployment, key | Their existing Azure agreement and private endpoint |
| `openai` | base URL, model, key | Simplest to start |
| `anthropic` | base URL, model, key | |

The key is a credential: encrypted per row, write-only, never returned, never logged — the same rule as a broker credential (NFR-003, [VOCABULARY §13](#13-credential-kinds)).

**`sendsPayloads`** is a boolean on the provider record, default `false`. With it off the assistant receives headers, death reasons and counts; with it on it receives bodies, and the drawer says so on every screen (THR-026).

## Document control

Revision 0.1 establishes the initial enumeration set. Values here are binding on the implementation, the API contract and the UI. Adding a value requires amending this file first, then reconciling every consumer that cites it.

*Amended November 2026:* §15 added. `captureState` had been defined only inside the contract's `QueueSummary` and used in three documents — an enum with no owner in the controlled vocabulary, which is the exact condition this file exists to prevent.
