---
document_id: DOC-ARCH-003
title: Provider Adapter Design
product: Marites
version: 0.1
date: 2026-11-09
status: Draft — design only; no adapter exists
---

# DOC-ARCH-003 — Provider Adapter Design

**Source:** [architecture overview](architecture-overview.md) §6; [vocabulary](../reference/vocabulary.md) §12–§14; ADR-015 to ADR-018. Owning document for how each provider satisfies the four ports.

The ports are the contract between the capture and operations code and a platform. This document records, per provider, **how each port is actually implemented** — because the ports exist precisely so those differences live here rather than in `if (provider == ...)` branches.

Nothing in this document has been run. Every statement is a design intention derived from the platform's documented model, and each adapter's section carries the item that must be confirmed before its milestone.

## 1. Port coverage

| Port | `rabbitmq` | `azure_service_bus` | `google_pubsub` |
|---|---|---|---|
| `IBrokerAdmin` | Management API over HTTP | `ServiceBusAdministrationClient` | `PublisherServiceApiClient` + `SubscriberServiceApiClient` |
| `IMessageTap` | Shadow queue bound to the exchange | Subscription on the topic | Subscription on the topic |
| `IMessagePublisher` | AMQP publish to an exchange | Send to a topic | Publish to a topic |
| `IMessageInspector` | Management API get + requeue | `PeekMessages` | Pull without ack, then `ModifyAckDeadline` |

All three satisfy all four. The differences are in **shape and blast radius**, not in presence — which is the reason the ports are split (ADR-015).

## 2. RabbitMQ — the reference implementation

Built first (M2), because it defines what the ports actually need.

| Concern | Implementation |
|---|---|
| Connection | AMQP 0-9-1 for data, management HTTP API for metadata. Two credential slots (`amqp`, `admin`), which may be the same user |
| Tap | Declare `marites.{tenant}.{hash}`, bind it to the watched exchange with the same routing key pattern, bound by `x-max-length` and `x-message-ttl` |
| Peek | `POST /api/queues/{vhost}/{name}/get` with `ackmode=reject_requeue_true` |
| Publish | Basic publish to the exchange with the original routing key |
| Discard one | Ack the delivery from the shadow queue — this removes the *copy*, not the original |
| Purge | `DELETE /api/queues/{vhost}/{name}/contents` |
| Replay | Republish each selected message to its original exchange and routing key, with an `x-marites-replay-of` header |
| Dead-letter mapping | `x-death[0].reason` → `deathReason`; the raw header array preserved in `nativeReason` |
| Redelivery signal | The AMQP `redelivered` flag |
| Permissions | `configure`/`write`/`read` on `marites.*`, plus `write` on watched exchanges (deployment §1.2) |

**The discard asymmetry is the one that needs a UI sentence.** Discarding a captured copy does not remove the message from the adopter's queue. Only a purge, or a discard executed against the live queue, changes the broker's state. Presenting both as "delete" would be a lie with consequences.

**Confirm before M2:** the quorum-queue delivery-limit effect of get-and-requeue (ASM-007, TC-010). If a peek can push a message toward the dead-letter exchange, peek on quorum queues is rate-limited per queue and labelled as not side-effect free.

## 3. Azure Service Bus

M5. The adapter that proves the ports were worth splitting, because every operation is a single API call — and none of them is the call RabbitMQ needed.

| Concern | Implementation |
|---|---|
| Connection | AMQP 1.0 through `Azure.Messaging.ServiceBus`; administration through `ServiceBusAdministrationClient` |
| Tap | One subscription per watched topic, named with the `marites.` prefix, with a short `DefaultMessageTimeToLive` so an abandoned subscription cannot accumulate |
| Peek | `PeekMessages` — non-destructive and lock-free, which makes it the cleanest peek of the three |
| Publish | Send to the topic |
| Discard one | `Complete` the peeked or received message on the shadow subscription |
| Purge | No purge operation exists. Deleting and recreating the subscription is the only route, which resets its position — a destructive act the UI must label as such |
| Replay | Republish each message to the original topic, preserving `MessageId`, `CorrelationId` and application properties |
| Dead-letter mapping | `DeadLetterReason` → `deathReason`, `DeadLetterErrorDescription` → `nativeReason`; `deliveryCount` carried into `deathCount` |
| Redelivery signal | `deliveryCount > 1` (ADR-019) |
| Permissions | Manage and receive on subscriptions, send on topics; managed identity preferred, because it stores no secret |

**Bare queues are not tappable.** A Service Bus queue has no fan-out — a second receiver competes. Only topics are watched, and the capability record says so ([VOCABULARY §12](../reference/vocabulary.md)). This is the one place where a provider genuinely cannot satisfy a port for an entity type, and the UI must say "topics only" rather than offering a queue that cannot work.

**Confirm before M5:** whether subscription deletion and recreation is acceptable to adopters as "purge". If it is not, purge is unavailable for this provider and the control is omitted — which is the honest outcome, not a disabled button.

## 4. Google Pub/Sub

M6. The only provider where replay is a rewind rather than a re-send, and the only one where purge is a seek.

| Concern | Implementation |
|---|---|
| Connection | gRPC through `Google.Cloud.PubSub.V1`; workload identity or a service-account JSON |
| Tap | One subscription per watched topic, `marites.` prefix, explicit ack deadline, retention capped |
| Peek | Pull with `returnImmediately`, then `ModifyAckDeadline` to release — a pull that does not ack |
| Publish | Publish to the topic, preserving ordering key where the topic uses one |
| Discard one | `Acknowledge` the message on the shadow subscription |
| Purge | `Seek` the subscription to the current time, which abandons the backlog |
| Replay | `Seek` the subscription to a timestamp or snapshot — **not** a per-message republish |
| Dead-letter mapping | `deliveryAttempt` plus the dead-letter topic's attributes → `deathReason`; the platform's own string in `nativeReason` |
| Redelivery signal | `deliveryAttempt > 1`, populated only when the subscription has a dead-letter policy |
| Permissions | `pubsub.subscriptions.create`/`delete`, `pubsub.subscriptions.consume`, `pubsub.topics.publish` |

**Seek is not replay of selected messages.** Rewinding a subscription redelivers everything from that point, including messages that were never dead-lettered, and it cannot be rate-limited per message. The control is labelled "Seek subscription to timestamp", states that the backlog will be redelivered, and the dry run cannot enumerate what will be reprocessed — so it must say that rather than show a count it cannot honour (DOC-UX-001 §3).

**Confirm before M6:** whether `deliveryAttempt` is exposed for the subscription shapes adopters use. Where it is not, counting for that profile is at-least-once, and the capability record must say so (ADR-019).

## 5. What every adapter must do

| Obligation | Why | Reference |
|---|---|---|
| Normalise death metadata into one `DeathInfo`, preserving the provider's string | Aggregation needs one vocabulary; debugging needs the original | ADR-016 |
| Report which ports it satisfies in the capability record | The UI gates on capabilities, not on a provider name | ADR-015 |
| Declare, never throw, an unsupported operation | A thrown exception becomes a `500`; a declared gap becomes an omitted control | DOC-ARCH-001 §6.1 |
| Bound everything it creates | A monitoring tool must not be the reason a broker fills | NFR-002 |
| Normalise the redelivery signal into one `Redelivered` field | The ledger is provider-independent | ADR-019 |
| Never modify an existing object in the adopter's topology | The product is a client, not a co-owner | ADR-001 |

## 6. What is deliberately not uniform

| Difference | RabbitMQ | Service Bus | Pub/Sub |
|---|---|---|---|
| Peek cost | Get + requeue, briefly blocks the queue | One call, non-destructive | Pull and release, consumes ack deadline |
| Replay semantics | Per-message republish | Per-message republish | Subscription rewind |
| Purge semantics | Queue contents deleted | Subscription deleted and recreated | Subscription seek |
| Dead-letter origin | DLX with `x-death` | DLQ sub-queue with a reason string | Dead-letter topic with delivery attempts |
| Watchable entity | Exchange and queue | Topic only | Topic only |

Five differences, three adapters, one capability record. If a future provider makes this table wider rather than longer, the ports are still right; if it forces a new column that changes the shape of an operation, that is the signal to revisit ADR-015 rather than to add a branch.

## 7. Document control

Revision 0.1 records per-adapter design for the three v1 providers. Written 09 November 2026 to close the gap named in the [architecture index](README.md). Each adapter carries one item to confirm before its milestone; none has been prototyped, and no provider SDK has been exercised.
