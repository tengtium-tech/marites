# Architecture

Version 0.4 | November 2026 | Status: Approved — Product owner, 2026-11-11

How the system is built across backend and frontend boundaries, why the tap is one hop, what each component does when it fails, and the decisions behind all three.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [C4 model](c4-model.md) | The system at three zoom levels: context, containers, components |
| [Architecture overview](architecture-overview.md) | Capture strategies, the one-hop tap, counting versus storing, contract-first boundary, module map, four ports, capability record, read and capture paths, identity, process topology, multiple clusters |
| [Backend DDD architecture](backend-ddd.md) | Bounded contexts, aggregate ownership, four backend layers, CQRS, transactions, repositories, domain events, and the outbox boundary |
| [API architecture and NFRs](api-architecture-and-nfrs.md) | API middleware, security, traffic and reliability, data performance, contract tooling, observability, and verification strategy |
| [Frontend architecture](frontend-architecture.md) | React Clean Architecture layers, dependency direction, generated-client boundary, error translation, and feature mapping |
| [Component failure modes](failure-modes.md) | Nine components, what each does when it fails, and what the operator sees while it is failing |
| [Provider adapter design](provider-adapters.md) | How RabbitMQ, Service Bus and Pub/Sub each satisfy the four ports, and the five places they deliberately differ |
| [Agent access](mcp-integration.md) | The MCP tool surface, why the credential rather than the tool list is the boundary, and the `CanExecute` grant that decides whether an assistant acts or only proposes |
| [Architecture decision register](decisions.md) | ADR-001 to ADR-025, including superseded decisions and the accepted backend Clean Architecture direction |

## The three arguments worth reading first

- **Overview §3, "one hop, not three."** The instinct is to insert a tap cluster between the adopter's broker and Marites. It costs a second RabbitMQ cluster to operate, adds two places to fail silently, and buys nothing an AMQP client cannot do.
- **Backend DDD §2–4.** Backend feature slices remain, with responsibilities separated across Domain, Application, Infrastructure, and API layers; persistence and provider-specific paths retain their own boundaries.
- **Failure modes §1, the failure that must not exist.** A monitor that stops monitoring and shows a plausible number is the product's worst outcome, and it is why `captureState` exists as a field rather than as an inference.

## Relationship to other sections

Architecture states boundaries. It does not restate requirements ([business](../02-business/requirements.md)), table structures ([data-and-contracts](../07-data-and-contracts/README.md)) or screen behaviour ([experience](../08-experience/README.md)).

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
