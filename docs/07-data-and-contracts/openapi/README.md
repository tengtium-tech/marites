# OpenAPI contracts

Version 0.1 | 09 November 2026 | Status: current

Two files, one contract. `openapi.yaml` is what the service serves; `openapi.deferred.yaml` holds operations that are designed and not yet served.

| File | Operations | Authority |
|---|---|---|
| [openapi.yaml](openapi.yaml) | 10 | **The product boundary.** The React client is generated from it and the service is measured against it |
| [openapi.deferred.yaml](openapi.deferred.yaml) | 15 | Designed and preserved. Not served, and not part of the boundary until promoted |

## Promoting an operation

Move the path, its parameters and any schemas it needs from the deferred file into `openapi.yaml`, delete them from the deferred file, then update the [API catalog](../api-catalog.md) row's revision and the [operation matrix](../../13-traceability/operation-coverage-matrix.md) milestone. A promotion is a move, not a rewrite.

## Which document wins

The contract is a rendering of the rules in [contract conventions](../contract-conventions.md), and a rendering of the values in the [vocabulary](../../14-reference/vocabulary.md). Where the contract and either disagree, the contract is corrected — it is never the place a rule is invented.

## Checking a change

Both files must parse as OpenAPI 3.1, every `$ref` must resolve within its own file, and no operation may be left untagged.
