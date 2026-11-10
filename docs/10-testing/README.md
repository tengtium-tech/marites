# Testing and verification

Version 0.3 | November 2026 | Status: Partial; verification is designed as 28 named cases. Nothing is written in code and nothing has been executed.

Trace expected behaviour to implementation evidence without equating document validation with application tests.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Verification plan](verification-plan.md) | Seven verification levels, 28 planned cases (TC-001 to TC-028) and eight milestone gates |
| [Traffic harness](traffic-harness.md) | The producer, the rejecting consumer, the nine scenarios and the manifest every assertion reads |
| [Requirement acceptance criteria](../02-business/requirements.md) (§3.1, §4.1) | One testable criterion per FR and NFR, with the milestone at which it can pass |
| [Operation coverage matrix](../13-traceability/operation-coverage-matrix.md) | Each of the 25 operations tied to its requirement, role, milestone, level and cases |
| [Contract conformance suite](../05-architecture/architecture-overview.md) (§5) | A black-box suite, `contract-tests/`, run over HTTP so it stays valid for any implementation of the contract |
| [Architecture test](../05-architecture/architecture-overview.md) (§6) | One ArchUnitNET test enforcing the project reference rules |
| [Failure modes](../05-architecture/failure-modes.md) | The component behaviours each case is designed to provoke |
| [Threat model](../09-security/threat-model.md) | The threats whose controls the security cases assert |

## Why verification got its own document

The section held an index and no other record until November 2026. Adding 24 cases, seven levels and eight gates made the index longer than the material it was indexing, which is the condition the [standard](../01-project/documentation-standard.md) names for creating a separate document. The index now indexes.

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
