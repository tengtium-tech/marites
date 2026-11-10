# Documentation areas

Version 0.1 | 07 November 2026 | Status: Drafted; the catalog maps areas to their owning documents. Per-area specification files are not written.

Organize Marites knowledge into cohesive documentation areas with responsibilities, exclusions, records and handoffs.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Documentation area catalog](domain-catalog.md) | Five areas: broker connections and providers, capture and counting, dead-letter recovery, reporting and metrics, access, roles and audit |
| [Architecture](../architecture/README.md) | Module map, capability model and the decisions behind the area boundaries |
| [Data model](../data-and-contracts/data-model.md) | The records each area owns |
| [Workflows](../workflows/README.md) | The three lifecycles that cross these areas |

## Work still needed

Marites has no commerce domain, so these areas are not separate specifications. Each is specified in the documents the catalog links. A dedicated area file is created only when its content outgrows those owners, and it must not restate them.

Use the [documentation structure](../project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
