# Marites

Marites observes message traffic on a broker an adopter already runs — RabbitMQ, Azure Service Bus or Google Pub/Sub — counts it exactly, shows why messages failed, and lets a support engineer retry, discard or publish from one screen. It is a **client** of that broker: it never provisions, hosts, upgrades or repairs one.

**Status: specification only. No application code exists, no test has run, and no broker has been contacted.**

## Where to start

| If you have | Read |
|---|---|
| Five minutes | [Requirements §2](docs/02-business/requirements.md) — the four-step recovery loop that scopes the product |
| Twenty minutes | [Portfolio overview](docs/01-project/portfolio-overview.md) — what this set demonstrates and what is weak in it |
| A specific question | [Documentation index](docs/README.md) — fourteen sections, each with an index that lists its own gaps |
| Doubt about the status | [Readiness dashboard](docs/12-delivery/readiness-dashboard.md) — one table, no percentages |

## What is in the repository

```
docs/                    the specification set: 14 sections, 48 substantive documents, 17 indexes
  01-project/            overview, decisions, risks, standard, structure
  02-business/           requirements and acceptance criteria
  03-workflows/          capture, recovery, onboarding, retention
  04-domains/            five documentation areas and their handoffs
  05-architecture/       the one-hop tap, failure modes, adapters, 25 decisions
  06-platform-foundation/ capture, identity, audit, configuration (index)
  07-data-and-contracts/ tables, dictionary, migrations, the OpenAPI contract
  08-experience/         metric rules, wireframes, screenshots, accessibility
  09-security/           threat model
  10-testing/            verification plan, traffic harness
  11-operations/         prerequisites, deploy, config, upgrade, backup, runbook
  12-delivery/           plan, CI, project board, readiness, review
  13-traceability/       operation-to-requirement matrix
  14-reference/          controlled vocabulary

prototype/       a UI exploration — React + shadcn/ui, no backend, not authoritative
```

**There is no application code**: no service, no database, no test. The contract at [docs/07-data-and-contracts/openapi/openapi.yaml](docs/07-data-and-contracts/openapi/openapi.yaml) is the artefact every later implementation will be measured against.

`prototype/` is a clickable mockup of the screens described in [the wireframes](docs/08-experience/wireframes-and-navigation.md), built with the stack the specification names so the design can be judged before it is built. It has no backend and every number on screen is hard-coded — where it disagrees with `docs/`, the documents win.

## How this becomes a sprint plan

The documents already contain the structure a project board needs, so the board is derived rather than invented:

| Board level | Derived from |
|---|---|
| Epic | A milestone, M0 to M7 — [deployment and onboarding §6](docs/11-operations/deployment-and-onboarding.md) |
| Story | An operation in the [API catalog](docs/07-data-and-contracts/api-catalog.md), or a named gap in a risk |
| Definition of done | The milestone's gate in [verification plan §3](docs/10-testing/verification-plan.md) |
| Test task | A case, TC-001 to TC-029, with its level and milestone |

Two rules keep the board from drifting away from the specification: **a story that needs data the contract does not serve is a contract change first**, and **a milestone does not close until its gate passes** — not until its tickets are closed.

## A note on what these documents are not

They are not evidence. Every verification case is marked `Planned`, the review is a self-review, and no behaviour has been observed. Where a document states what the system will do, it describes intent — and the set is written so that a reader can tell the difference without being told.

## Contributing

Not yet open. The specification is at documentation baseline 0.19; changes to an accepted decision require a superseding record rather than an edit — see [the documentation standard](docs/01-project/documentation-standard.md).

## Licence

**MIT** — see [LICENSE](LICENSE). Use it, fork it, sell a product built on it; keep the copyright notice.

MIT rather than Apache-2.0 because the difference that matters here is the explicit patent grant, and nothing in this design reads a patent. If that changes, Apache-2.0 is a one-commit replacement. The copyright line names "Marites contributors" so it does not have to be edited per author; put your own name in it if you prefer.

The choice is recorded as [DEC-009](docs/01-project/decisions-and-open-questions.md), because a licence is the one project decision an adopter inherits with the software — the same reasoning that rejected a copyleft dependency in ADR-013.
