# Architecture diagrams

Version 0.1 | November 2026 | Status: Approved — Product owner, 2026-11-11.

The rendered figures for the [architecture overview](../architecture-overview.md), [C4 model](../c4-model.md), [frontend architecture](../frontend-architecture.md), [backend DDD](../backend-ddd.md), [API architecture and NFRs](../api-architecture-and-nfrs.md), and [MCP integration](../mcp-integration.md). Each `.png` is built from the `.json` beside it: the JSON is the editable source, and the PNG is its inline preview.

| Source JSON | Figure |
|---|---|
| `01-architecture-baseline.architecture.json` | Overview §1, runtime topology and worker decomposition |
| `one-hop-tap.architecture.json` | Overview §3, direct broker → worker → store path |
| `read-path.sequence.json` | Overview §7, stored-message query |
| `capture-batch.sequence.json` | Overview §7, batch transaction and acknowledgement order |
| `multiple-clusters.architecture.json` | Overview §10, independent workers and shared history |
| `data-stores.architecture.json` | Data model, ownership across the two stores and object storage |
| `c4-context.architecture.json` | C4 §1, system context |
| `c4-containers.architecture.json` | C4 §2, containers and the two stores |
| `c4-capture-components.architecture.json` | C4 §3, capture worker components |
| `api-structured-query.sequence.json` | API Architecture §7, structured message query |
| `api-recovery-confirmed.sequence.json` | API Architecture §7, confirmed recovery command |
| `api-recovery-ambiguous.sequence.json` | API Architecture §7, ambiguous result and reconciliation |
| `backend-ddd-outbox.sequence.json` | Backend DDD §6, command and conditional outbox delivery |
| `frontend-dependency-direction.architecture.json` | Frontend Architecture §2, inward dependencies and composition root |
| `frontend-request-flow.sequence.json` | Frontend Architecture §2, request and response across client layers |
| `mcp-two-paths.workflow.json` | MCP Integration §4, credential-selected execution or proposal |

## What is deliberately not a diagram

The other fenced blocks in the overview are a directory tree (`src/`), two type definitions (`ProviderCapabilities`, `DeathInfo`), a slice listing and a one-line read path. They are code and lists, and drawing them would make them harder to read, not easier.
