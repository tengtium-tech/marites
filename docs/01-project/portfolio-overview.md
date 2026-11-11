# Portfolio overview

Version 0.2 | November 2026 | Status: Approved — Product owner, 2026-11-11

What this repository is, what it is meant to demonstrate, and what it deliberately does not prove. Written for a reader who has twenty minutes and wants to know whether the thinking is sound.

## 1. What this is

**A documentation-led repository for a product that does not exist yet.** It includes a React prototype to illustrate the proposed experience, but no production application, database, test or deployment. The specifications describe intent.

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

The set distinguishes five things that are routinely conflated: **requirement, technical proposal, designed verification, executed evidence, sign-off.** The project owner has approved the documentation baseline; no application verification case has executed, and no implementation or release sign-off exists. Every verification case is marked `Planned`, and the [readiness dashboard](../12-delivery/readiness-dashboard.md) keeps that state separate.

That is why the requirement traceability reads "TC-014" rather than "verified", and why the review calls itself a self-review.

### 2.4 Counts that can be checked

Every claim of the form "N operations" or "N decisions" is checkable against the file that owns it, and `tools/docs_check.py` checks them: links, anchors, identifiers and stated totals, in one command. The honest part is what it does not do — it is not wired to a pipeline yet, so it runs when someone runs it, which is how the defects this review found survived two earlier passes.

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
| Working software | A React prototype exists ([screenshots](../08-experience/screenshot-index.md), thirty-nine of them) but it is a demonstration of the design, not the product. No backend, no broker connection, no tests |
| Brand and marketing | The palette and type scale are defined ([visual design](../08-experience/visual-design.md)) because a UI needed them. There is no logo system, no website, no launch material |
| Business model, pricing, go-to-market | Out of scope for a technical specification, and inventing them would add noise to the decisions |

## 5. An honest assessment

**Strong:** the reasoning about counting versus storing, the refusal to present an estimate as exact, the treatment of "zero versus blind", and the willingness to record reversals. The threat model names what it accepts rather than only what it mitigates.

**Weak, and named as weak in the documents:**

- **Nothing is proven.** The counting claim rests on ADR-019 and ADR-011, both of which need integration tests that do not exist (TC-013, TC-014).
- **The counting claim is designed, not proven.** Compaction is idempotent by recomputation and by transaction scope ([ADR-024](../05-architecture/decisions.md)), and redelivery is absorbed by a ledger ([ADR-019](../05-architecture/decisions.md)) — but neither has run, and a design that is right on paper is still a design.
- **The isolation story has a hole.** Both stores rely on explicit tenant scope in Application repositories; the batched message store has no row-level security because its writer batches across tenants ([migration plan](../07-data-and-contracts/migration-plan.md) §4).
- **Operationally it is deaf.** Health and readiness endpoints and a Prometheus endpoint exist ([deployment §3.2](../11-operations/deployment-and-onboarding.md)), but nothing watches them and nothing pages anyone. A stopped capture is discovered by a person noticing, which is the failure the product exists to prevent.
- **The documentation is large relative to what exists.** Forty-eight substantive documents and sixteen section or nested indexes for zero lines of production code is a real risk: the set can look finished when nothing is built. The readiness dashboard exists to counter that, and it is the page to distrust least.

## 6. What a reviewer should check first

1. **Does the contract actually reflect the decisions?** DEC-002 should produce a flat refusal on message endpoints, not a redacted variant; DEC-005 should produce a default that works unconfigured; DEC-006 should produce base64 of exact bytes and no masking feature anywhere.
2. **Are the counts right?** 10 served operations, 15 deferred, 25 ADRs, 19 decisions, 29 FR, 8 NFR, 27 error codes, 12 current tables. Each is checkable.
3. **Is anything claimed as verified?** It should not be. Search for "Verified" and expect to find only a vocabulary definition.
4. **Do the two places that describe the same thing agree?** The review found several that did not. Finding one more is a fair outcome.

## 7. The implementation portfolio

Marites is one product with a shared specification and multiple implementation profiles. Requirements, workflows, domain rules, user-visible behavior, the HTTP contract, and acceptance criteria are the common source of truth. Each implementation must conform to them; a stack choice does not silently change product behavior.

The first profile is React/TypeScript with a C#/.NET backend. The planned portfolio covers the full three-by-three pairing matrix:

| Frontend \ Backend | C#/.NET | Python | Go |
|---|---|---|---|
| React | First profile | Planned | Planned |
| Angular | Planned | Planned | Planned |
| Svelte | Planned | Planned | Planned |

The current approved architecture set contains stack-specific guidance alongside shared rules. React frontend architecture, ASP.NET Core API guidance, C#/.NET backend architecture, provider/storage adapters, deployment, and CI choices describe the initial profile. Before another profile starts, its implementation guidance must be separated from shared behavior so a different stack can follow the same contract without inheriting framework-specific instructions.

The repository will keep shared specifications under `docs/` and implementation code under `implementations/`, grouped by frontend and backend technology. Short-lived feature branches are for changes in progress; long-lived technology variants live in their directories so the shared contract and completed implementations remain visible together on `main`.

Each backend profile gets a GitHub Project. The first project covers React + .NET; later Python and Go projects reuse the same requirements, contract, and verification gates. Frontend pairings are tracked as implementation work against each backend's conformance suite.

## 8. Document control

Revision 0.2 records the multi-stack portfolio direction. It makes no claim about quality beyond what the linked documents support, and it is the only document in the set written primarily for a reader outside the project.
