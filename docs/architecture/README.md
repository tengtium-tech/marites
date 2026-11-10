# Architecture

Version 0.4 | 09 November 2026 | Status: shared documentation section and coverage index.

How the system is built, why the tap is one hop, what each component does when it fails, and the decisions behind all three.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [C4 model](c4-model.md) | The system at three zoom levels: context, containers, components |
| [Architecture overview](architecture-overview.md) | Capture strategies, the one-hop tap, counting versus storing, contract-first boundary, module map, four ports, capability record, read and capture paths, identity, process topology, multiple clusters |
| [Component failure modes](failure-modes.md) | Nine components, what each does when it fails, and what the operator sees while it is failing |
| [Provider adapter design](provider-adapters.md) | How RabbitMQ, Service Bus and Pub/Sub each satisfy the four ports, and the five places they deliberately differ |
| [Agent access](mcp-integration.md) | The MCP tool surface, why the credential rather than the tool list is the boundary, and the propose-and-approve model |
| [Architecture decision register](decisions.md) | ADR-001 to ADR-019, all accepted, each with rationale and alternatives rejected |

## The three arguments worth reading first

- **Overview §3, "one hop, not three."** The instinct is to insert a tap cluster between the adopter's broker and Marites. It costs a second RabbitMQ cluster to operate, adds two places to fail silently, and buys nothing an AMQP client cannot do.
- **Overview §6, vertical slices with four ports across three inversion seams.** Only the broker is genuinely swappable. PostgreSQL is a commitment and the HTTP boundary is protected by the contract, so dependency inversion is applied at the three seams where it earns its keep, while the four ports keep provider differences inside their adapters.
- **Failure modes §1, the failure that must not exist.** A monitor that stops monitoring and shows a plausible number is the product's worst outcome, and it is why `captureState` exists as a field rather than as an inference.

## Relationship to other sections

Architecture states boundaries. It does not restate requirements ([business](../business/requirements.md)), table structures ([data-and-contracts](../data-and-contracts/README.md)) or screen behaviour ([experience](../experience/README.md)).

## Work still needed

- **Runtime topology under load.** [Deployment](../operations/deployment-and-onboarding.md) §3 covers what ships; the behaviour of API, worker and database under load is unmeasured. TC-012 asserts the target and nothing below it.
- **Compaction idempotency.** [Failure modes](failure-modes.md) §6 flags a retried compaction over additive upserts as unspecified. It is the most serious open design item in the section: a double aggregation would inflate a total permanently, and totals are never rebuilt from rows.
- **An MCP server.** [Agent access](mcp-integration.md) specifies the surface; nothing implements it, and no tool has been called.
- **Quantified thresholds.** "After two missed intervals" and similar statements are illustrative. The real values are implementation constants and are not set.

Use the [documentation structure](../project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
