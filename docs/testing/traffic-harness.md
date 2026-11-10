---
document_id: DOC-TEST-002
title: Traffic Harness
product: Marites
---

# Traffic harness

Every case in the [verification plan](verification-plan.md) asserts something about traffic: a count, a dead-letter reason, a captured body, a replay that succeeds. None of it can be tested without a broker that has known traffic in it, and **the set has never had one** — §5 of the verification plan said so: *"no broker topology, sample message set or seed data is committed. Fixtures would be invented, and an invented fixture tests the fixture."*

This document defines the thing that fixes that: a small tool, shipped in the repository, that puts known traffic into a broker and then does something deliberate with it.

## 1. Two roles, because one is not enough

| Role | Command | What it does |
|---|---|---|
| **Producer** | `fixtures produce` | Publishes a scenario's messages at a controlled rate, and writes a manifest of what it sent |
| **Consumer** | `fixtures consume` | Consumes and then **rejects on purpose**, so messages reach a dead-letter queue |

**The consumer is the half that is easy to forget.** A producer alone fills a queue with healthy messages; nothing dead-letters, so the dead-letter list, the reason breakdown, the replay path and half the audit have nothing to act on. Dead letters are produced by a *consumer that fails*, which is why the harness ships one.

**The consumer rejects in the same ways the product classifies.** The reason vocabulary has six values ([VOCABULARY §4](../reference/vocabulary.md)); a harness that only ever `nack`s exercises one of them:

| Mode | How | Produces |
|---|---|---|
| `reject` | `nack` without requeue / `abandon` / `nack` | `rejected` |
| `expire` | publish with a TTL that elapses before consumption | `expired` |
| `overflow` | fill past `max-length` | `maxlen` |
| `unroutable` | publish with a routing key nothing binds | `no_route` |
| `crash` | exit mid-batch, leaving messages unacked | `delivery_limit` after redelivery |
| `accept` | ack normally — the control group | none |

## 2. Scenarios

A scenario is a file, not code. Each one names what to publish, how the consumer behaves, and what the run is expected to prove.

| Scenario | Publishes | Consumer | What it proves |
|---|---|---|---|
| `healthy-burst` | 1,000 well-formed | `accept` | Counts, rollups and rates with no failures — the baseline every other scenario is measured against |
| `poison-subset` | 1,000, of which 40 are rejected | `reject` | Dead-letter capture, reason classification, the dead-letter rate |
| `consumer-down` | 1,000 with no consumer running | — | Depth growth with an empty DLQ — the FR-018 gap, and the honest "no dead-letter routing" state |
| `duplicate-storm` | 500 distinct, each published 4 times | `accept` | The redelivery ledger (ADR-019): 2,000 delivered, 500 counted |
| `large-bodies` | 1 KB, 60 KB, 64 KB, 65 KB, 1 MB | `accept` | The inline threshold, and the object-store gap that is still unresolved (RSK-013) |
| `unicode-and-binary` | UTF-8, emoji, embedded NUL, invalid UTF-8 | `accept` | Encoding safety end to end — storage, display, and the payload dialog |
| `slow-consumer` | 1,000 at 20/s consumption | `accept`, rate-limited | Backlog, depth-versus-rate, the stale capture state |
| `replay-target` | 200 rejected, consumer fixed mid-run | `reject` then `accept` | The whole recovery loop: dead-letter, replay, succeed, audit with evidence |
| `perf-sustained` | 5,000/s for a configurable duration | `accept` | NFR-001, and where the curve stops being linear |

**`replay-target` is the one that matters most**, because it is the only scenario that exercises the product's actual claim: a message fails, an operator sees why, replays it, and it succeeds — with a record of both bodies.

## 3. Determinism, and the manifest

A test that asserts "about 40 dead letters" is not a test. The harness is deterministic by construction:

- **A seed.** Every random choice — body content, which messages are poison, inter-arrival jitter — derives from `--seed`, default `1`. The same seed produces the same messages on every provider.
- **A manifest.** The producer writes `manifest.json` beside the run: one entry per message with `index`, `messageId`, `contentHash`, `bodySize`, `expectedOutcome` (`accepted` | `rejected` | `expired` | …).
- **Assertions read the manifest, not the scenario.** A case asserting *"the dead-letter list contains exactly the messages the manifest marks rejected"* is falsifiable. A case asserting *"some messages dead-lettered"* is not.

```json
{
  "scenario": "poison-subset",
  "seed": 1,
  "provider": "rabbitmq",
  "startedAt": "2026-11-10T15:40:02Z",
  "published": 1000,
  "messages": [
    { "index": 0, "messageId": "fx-1-0", "contentHash": "9f2c…", "bodySize": 412, "expectedOutcome": "accepted" },
    { "index": 7, "messageId": "fx-1-7", "contentHash": "3a81…", "bodySize": 398, "expectedOutcome": "rejected" }
  ]
}
```

## 4. One scenario, three providers

The same scenario file runs against RabbitMQ, Service Bus and Pub/Sub. The harness has one adapter per provider, and the scenario declares what it needs rather than what it does:

| Need | RabbitMQ | Service Bus | Pub/Sub |
|---|---|---|---|
| Publish | exchange + routing key | topic or queue | topic |
| Reject | `basic.nack` | `abandon` | `modack` |
| Dead-letter routing | DLX on the queue | native, on the subscription | native, with a dead-letter policy |
| Delay / TTL | per-message TTL | scheduled enqueue | not supported |

**A scenario declares `requires: [deadLetterRouting, perMessageTtl]`.** If the target provider cannot satisfy a requirement, the harness **refuses to run** rather than producing a different shape of traffic and letting the test pass for the wrong reason. A capability that silently degrades is how a test suite becomes decorative.

## 5. How it runs

The harness is a container in the same compose file as the broker stack, so there is no "install this first":

```bash
docker compose -f testing/compose.yaml up -d rabbitmq
docker compose -f testing/compose.yaml run --rm fixtures produce --scenario poison-subset --seed 1
docker compose -f testing/compose.yaml run --rm fixtures consume --mode reject --duration 30s
```

In CI it is the same commands as one-shot containers. Locally, `make seed SCENARIO=poison-subset` wraps them.

**The broker stack and the harness are configuration, not code.** The topologies are the thing under test, so they are readable as files — a compose service and a scenario — rather than buried in test setup. This is also why the database side uses testcontainers while the broker side does not: a database is a dependency, a broker topology is the subject.

## 6. Test data rules

| Rule | Why |
|---|---|
| **Bodies are generated, never copied** | A captured production payload in a repository is a data-protection incident waiting for a clone. Every body is built from a template and a seed |
| **No real customer names, ids or amounts** | The templates use `acme-pty`, `ORD-88213` and round numbers — synthetic by inspection |
| **The harness refuses an external body file** | A scenario may not reference a path outside its own directory, so "just drop this export in" is not possible |
| **Payloads stay small except in `large-bodies`** | A test run should not fill a developer's disk, and the large cases are named so they are chosen deliberately |

## 7. What it is not

- **Not a load generator for the broker.** `perf-sustained` measures Marites against NFR-001; it does not benchmark RabbitMQ, and its numbers say nothing about a broker's ceiling.
- **Not the adopter's traffic.** A harness that produced realistic traffic would be a different product. This one produces *known* traffic, which is the only kind a test can assert against.
- **Not a demo seeder for sales.** It could be used that way, and the manifest makes it tempting, but it exists to make cases falsifiable.

## 8. Document control

Revision 0.1, 10 November 2026. Written to close the gap the verification plan named in its own §5: no fixtures were committed, so no case could be run. Nothing here is implemented — the commands are the specification for a tool that does not exist yet, and the scenario names are the contract the first implementation must satisfy. The three-provider parity in §4 is derived from each provider's documented semantics, not from an observed run.
