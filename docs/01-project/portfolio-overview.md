# Portfolio overview

Version 0.1 | November 2026 | Status: current

What this repository is, what it is meant to demonstrate, and what it deliberately does not prove. Written for a reader who has twenty minutes and wants to know whether the thinking is sound.

## 1. What this is

**A documentation-only repository for a product that does not exist yet.** There is no application code, no database, no test and no deployment. Every document describes intent.

Marites observes message traffic on a broker the adopter already runs — RabbitMQ, Azure Service Bus or Google Pub/Sub — counts it exactly, shows why messages failed, and lets a support engineer retry, discard or publish from one screen. It is a client of the broker, never its owner ([ADR-001](../05-architecture/decisions.md)).

## 2. What it demonstrates

The subject is a dead-letter monitor. The **artefact under review is the specification set**, and four things in it are the point.

### 2.1 A contract written before the code, and treated as the product boundary

`07-data-and-contracts/openapi/openapi.yaml` is written, reviewed and scoped — 10 operations served, 15 deferred — before a single endpoint exists. The React client is generated from it, so a renamed field breaks a build instead of a runtime request. Conventions that most APIs discover by accident are decided in prose first: outcome classes, idempotency, revision guards, typed confirmation, paging, versioning ([DOC-API-001](../07-data-and-contracts/contract-conventions.md)).

The discipline that makes it more than a file: **where the contract and a convention disagree, the convention wins and the contract is corrected.** The contract is a rendering of rules, not the place rules are invented.

### 2.2 Decisions recorded, including the ones that were reversed

Twenty-three architecture decisions and nineteen project decisions, each with the alternative that was rejected and why. Two are worth reading for the reasoning rather than the outcome:

- **ADR-013** rejects a third-party mediator on licensing grounds — MediatR is dual-licensed under a strong copyleft or a commercial agreement, and for software distributed to self-hosting adopters the obligation lands on every customer. The replacement is a fifty-line dispatcher, with a written guard against it growing into the framework it replaced.
- **DEC-008** rejects cross-cluster deduplication, not because it is hard but because it **cannot span time ranges**: rows expire, rollups are already aggregated, so a "unique messages" figure would mean one thing for a day and something else for a year. A number that is honest at every range beats one that is precise at one.

A decision log that only records the answers is a changelog. These record the arguments.

### 2.3 Evidence categories that are kept apart

The set distinguishes five things that are routinely conflated: **requirement, technical proposal, designed verification, executed evidence, sign-off.** Only the first three exist here. Every verification case is marked `Planned`, and the [readiness dashboard](../12-delivery/readiness-dashboard.md) says so in the first paragraph.

That is why the requirement traceability reads "TC-014" rather than "verified", and why the review calls itself a self-review.

### 2.4 Counts that can be checked

Every claim of the form "N operations" or "N decisions" is checkable against the file that owns it. The contract is parsed and its references resolved on every change.

This sounds like hygiene. It is the mechanism that keeps a data model and a contract from drifting apart — the failure that surfaces as a broken update path in the first sprint, not as a compile error.

## 3. How to read it in twenty minutes

| Minutes | Read | Why |
|---|---|---|
| 0–3 | [Requirements](../02-business/requirements.md) §2 | The four-step recovery loop, and the test that scopes the product: a feature that serves none of Detect, Understand, Act, Prove is not MVP |
| 3–6 | [Architecture overview](../05-architecture/architecture-overview.md) §2–§3 | Why a second queue bound to the exchange is the only non-destructive way to observe AMQP, and why a tap cluster is the wrong answer |
| 6–10 | [Data model](../07-data-and-contracts/data-model.md) §3 | Counting and storing are separate problems. Exact counts forever, rows for a window |
| 10–15 | [Contract conventions](../07-data-and-contracts/contract-conventions.md) §3–§4 | The rules, and the one outcome class most APIs get wrong: `Unresolved` is `202`, not `503` |

If you only read one page, read the [failure modes](../05-architecture/failure-modes.md) §1. A monitoring product's worst output is a plausible number, and that section is the design's answer to it.

## 4. What is deliberately absent

| Absent | Why |
|---|---|
| Application code | The artefact is the specification. Code would be a second, less reviewable claim about the same design |
| Tests and results | Nothing has run, so nothing is claimed. Every case is Planned |
| Wireframe images | Layout is described in text ([wireframes](../08-experience/wireframes-and-navigation.md)) so it can be reviewed in a diff |
| Visual design | Not designed. Contrast requirements are stated against a palette that does not exist, and the document says so |
| Business model, pricing, go-to-market | Out of scope for a technical specification, and inventing them would add noise to the decisions |

## 5. An honest assessment

**Strong:** the reasoning about counting versus storing, the refusal to present an estimate as exact, the treatment of "zero versus blind", and the willingness to record reversals. The threat model names what it accepts rather than only what it mitigates.

**Weak, and named as weak in the documents:**

- **Nothing is proven.** The counting claim rests on ADR-019 and ADR-011, both of which need integration tests that do not exist (TC-013, TC-014).
- **One design item is genuinely unresolved.** A retried compaction over additive upserts would double a total permanently, and no guard is specified ([failure modes §6](../05-architecture/failure-modes.md)).
- **The isolation story has a hole.** Row-level security covers configuration tables but not the capture tables, because the write path batches across tenants ([migration plan](../07-data-and-contracts/migration-plan.md) §4).
- **Operationally it is blind.** No alerting, no health endpoint, no way to watch Marites with Marites. Every runbook symptom is discovered by a person.
- **The documentation is large relative to what exists.** Forty-one documents and sixteen indexes for zero lines of code is a real risk: the set can look finished when nothing is built. The readiness dashboard exists to counter that, and it is the page to distrust least.

## 6. What a reviewer should check first

1. **Does the contract actually reflect the decisions?** DEC-002 should produce a flat refusal on message endpoints, not a redacted variant; DEC-005 should produce a default that works unconfigured; DEC-006 should produce base64 of exact bytes and no masking feature anywhere.
2. **Are the counts right?** 10 served operations, 15 deferred, 24 ADRs, 19 decisions, 29 FR, 8 NFR, 26 error codes, 12 tables. Each is checkable.
3. **Is anything claimed as verified?** It should not be. Search for "Verified" and expect to find only a vocabulary definition.
4. **Do the two places that describe the same thing agree?** The review found several that did not. Finding one more is a fair outcome.

## 7. Document control

Revision 0.1 records the portfolio framing, written November 2026. It makes no claim about quality beyond what the linked documents support, and it is the only document in the set written primarily for a reader outside the project.
