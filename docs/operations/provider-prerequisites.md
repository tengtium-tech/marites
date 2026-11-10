---
document_id: DOC-OPS-002
title: Provider Prerequisites
product: Marites
version: 0.1
date: 2026-11-09
status: Draft — least-privilege sets designed; no provider has been configured
---

# DOC-OPS-002 — Provider Prerequisites

**Source:** [deployment and onboarding](deployment-and-onboarding.md) §1.5–§1.6, §2. Owning document for the permissions Service Bus and Pub/Sub adopters must grant, and the commands that prove they were granted.

RabbitMQ's grants are in [deployment §1.2](deployment-and-onboarding.md) because that document carries the reference implementation. This document carries the other two providers, which the deployment document held as a draft table until 09 November 2026.

**What Marites creates in each platform is owned by [deployment §4.1](deployment-and-onboarding.md); the cost of a capture stream by [§1.5](deployment-and-onboarding.md).** This document adds only the identity, the roles and the verification.

## 1. Azure Service Bus

### 1.1 Choose the identity first

| Option | Stores a secret | When to use |
|---|---|---|
| **Managed identity** (`managed_identity`) | **No** | Default wherever the broker runs in Azure and the Marites host can hold an identity — App Service, Container Apps, AKS with workload identity, or a VM |
| Service principal (`entra_service_principal`) | Yes — client secret | Host outside Azure, or where managed identity is unavailable |
| SAS policy key (`sas_key`) | Yes — policy key | Legacy namespaces, or a platform team that already issues SAS policies and prefers to keep doing so |

Managed identity is listed first because it removes a credential from the system entirely: nothing to rotate, nothing to leak, nothing in `credentials_enc` ([VOCABULARY §13](../reference/vocabulary.md)). An adopter who chooses a SAS key is choosing to hold a secret that Marites must store.

### 1.2 The rights required

| Scope | Right | Needed for |
|---|---|---|
| Namespace or topic | Create and delete subscriptions | Declaring the `marites.` subscription per watched topic |
| Subscription | Receive and settle | Consuming copies, peeking, discarding a copy |
| Topic | Send | Replay and publish |
| Subscription | Read dead-letter sub-queue | Reading `DeadLetterReason` and `DeadLetterErrorDescription` |

In Azure RBAC terms the predefined role that covers all four is **Service Bus Data Owner** on the namespace. It is broader than the product needs — it also permits deleting the namespace's entities — so where an adopter's policy allows it, a custom role with the four data actions above is the better grant. Sending and receiving alone (Data Sender plus Data Receiver) is **not sufficient**: neither includes subscription management, so capture cannot be enabled.

**Bare queues are not watched.** A Service Bus queue has no fan-out, so a second receiver competes with production. Only topics are captured, and the capability record says so ([VOCABULARY §12](../reference/vocabulary.md)).

### 1.3 Verify it

```bash
# Can the identity see the namespace at all?
az servicebus namespace show --name <namespace> --resource-group <rg> --query id

# Does it hold the role at the right scope?
az role assignment list --scope <namespace-id> --assignee <object-id> --query "[].roleDefinitionName"

# Can it create and delete a subscription? The real test, and the one preflight performs.
az servicebus topic subscription create \
  --namespace-name <namespace> --topic-name <topic> \
  --name marites.probe --resource-group <rg>
az servicebus topic subscription delete \
  --namespace-name <namespace> --topic-name <topic> \
  --name marites.probe --resource-group <rg>
```

If the create succeeds and the delete fails, capture works and cleanup does not — which leaves a subscription behind on every disable. Treat delete as part of the grant, not as housekeeping.

## 2. Google Pub/Sub

### 2.1 Choose the identity first

| Option | Stores a secret | When to use |
|---|---|---|
| **Workload identity** (`workload_identity`) | **No** | GKE, Cloud Run, or any host federated to Google Cloud |
| Service account JSON (`service_account_json`) | Yes — a key file | Host outside Google Cloud, or a platform team that issues keys centrally |

A downloaded service-account key is a long-lived credential in a file. Where workload identity is available, the key is strictly worse and should not be offered as the default.

### 2.2 The rights required

| Right | Needed for |
|---|---|
| `pubsub.subscriptions.create` | Declaring the `marites.` subscription per watched topic |
| `pubsub.subscriptions.delete` | Cleaning up when capture is disabled |
| `pubsub.subscriptions.get` | Reading subscription configuration and backlog |
| `pubsub.subscriptions.consume` | Pulling copies, peeking, discarding |
| `pubsub.topics.get` | Reading topic metadata |
| `pubsub.topics.publish` | Replay and publish |
| `pubsub.topics.attachSubscription` | Attaching a subscription to a topic that already exists |

The predefined roles do not split the way an adopter expects: **Pub/Sub Subscriber** covers consume but not subscription management, and **Pub/Sub Editor** covers management but is broader than needed across the project. The honest recommendation is a **custom role** carrying exactly the seven permissions above, scoped to the topics Marites watches.

**`pubsub.topics.attachSubscription` is the one that gets missed.** Creating a subscription on a topic is an operation against the *topic*, so a role that grants only `pubsub.subscriptions.*` fails at the last step with a permission error that names the topic — which reads as a topic problem and is not.

### 2.3 Verify it

```bash
# What does the identity actually hold?
gcloud projects get-iam-policy <project> \
  --flatten="bindings[].members" \
  --filter="bindings.members:<principal>" \
  --format="table(bindings.role)"

# Can it create, pull from, and delete a subscription? Preflight performs this sequence.
gcloud pubsub subscriptions create marites.probe --topic <topic>
gcloud pubsub subscriptions pull marites.probe --limit 1 --auto-ack
gcloud pubsub subscriptions delete marites.probe
```

The pull is included deliberately: creating a subscription proves management rights, and pulling proves data rights. A grant that passes the first and fails the second is a common half-configuration.

## 3. Preflight mapping

Each check in [deployment §2](deployment-and-onboarding.md) has an equivalent on both providers. The check identifiers are the contract's, and they are the same six for every provider — only the underlying call differs.

| Check | Service Bus | Pub/Sub |
|---|---|---|
| `management_api_reachable` | Administration client can read the namespace | Subscriber client can get a topic |
| `vhost_visible` | Namespace resolves for the identity | Project resolves for the identity |
| `topology_write_permitted` | Create and delete a probe subscription | Create, pull and delete a probe subscription |
| `dead_letter_configured` | Per subscription: dead-lettering is enabled and a target queue is set | Per subscription: a dead-letter topic and `maxDeliveryAttempts` are set |
| `shadow_declarable` | Probe subscription can be created with a TTL | Probe subscription can be created with a bounded retention |
| `clock_skew` | Host clock within five minutes of the broker's `ServerTime` | Host clock within five minutes of the Google API's `Date` header |

**`dead_letter_configured` behaves differently from RabbitMQ, and the difference matters.** On Service Bus a dead-letter sub-queue always exists, so the check passes even when nothing routes there deliberately; on Pub/Sub it fails until a dead-letter policy is set. Both are reported per queue rather than per broker, and neither may render as `0` ([ERR-QUE-002](../data-and-contracts/error-catalog.md)).

## 4. What Marites does not need, and should not be granted

| Not needed | Why it is worth saying |
|---|---|
| Creating or deleting topics, queues or namespaces | Marites is a client (ADR-001). A grant that permits it is a grant that could destroy the adopter's topology |
| IAM or role administration | The product never assigns cloud roles; its own roles are application roles |
| Namespace-wide send or manage | Scope to the watched topics. A namespace-wide send right is a publish right on everything |
| Billing or monitoring APIs | Cost is reported by the platform, not read by Marites |

An adopter's security team should be able to read this table and reduce the grant, not merely approve it.

## 5. Limits worth sizing before enabling capture

Both platforms impose finite subscription counts, and capture consumes one per watched topic.

| Limit | Shape of the problem |
|---|---|
| Subscriptions per topic (Service Bus) | A namespace with many topics multiplies the count. Check the current quota before enabling capture on a topic-dense namespace |
| Subscriptions per project (Pub/Sub) | Same multiplication, at project scope |
| Message retention (Pub/Sub) | A subscription's retention bounds how far back a backfill can reach. Beyond it, only counts survive |
| Subscription TTL (Service Bus) | A subscription with no receiver can be auto-deleted by idle settings. Marites sets its own TTL so an abandoned subscription cannot accumulate |

No numeric quota is quoted here on purpose: the values change, and a stale number in a specification is worse than a pointer to the provider's own page. Verify at the time of onboarding.

## 6. Document control

Revision 0.1 promotes the draft table in deployment §1.6 into a document with identities, rights, verification commands and preflight mapping. Written 09 November 2026. No Service Bus namespace or Pub/Sub project has been configured, no command above has been run against a real platform, and every permission list is a design intent derived from the platforms' documented role models.
