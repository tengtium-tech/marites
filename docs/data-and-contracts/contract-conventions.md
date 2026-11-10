---
document_id: DOC-API-001
title: Contract Conventions
product: Marites
version: 0.1
date: 2026-11-07
status: Draft — binding on data-and-contracts/openapi/openapi.yaml
---

# DOC-API-001 — Contract Conventions

**Source:** [requirements](../business/requirements.md) FR-019, [architecture](../architecture/architecture-overview.md) §5. Owning document for the rules the API contract follows.

`data-and-contracts/openapi/openapi.yaml` is a **rendering of these rules**, not the place they are invented. Where the contract and this document disagree, this document wins and the contract is corrected.

## 1. Selected choices

| Choice | Detail |
|---|---|
| Specification | OpenAPI 3.1, a single document at `data-and-contracts/openapi/openapi.yaml` |
| Format | YAML. More conventional for OpenAPI and reviews better in a pull request; JSON is a mechanical conversion if a tool needs it |
| Path prefix | `/api/v1` — the major version lives in the path |
| Transport | HTTPS only. SSE for live updates, on the same origin |
| Errors | `application/problem+json` (RFC 9457), codes owned by [DOC-API-003](error-catalog.md) |
| Client generation | The React client is generated from the contract. Hand-written request types are a defect |

One document rather than one per area: Marites is a single service, and splitting it would invent boundaries the system does not have.

## 2. Data conventions

- **Identifiers.** UUID strings, assigned by the server, never reused. The client treats them as opaque and never parses them.
- **Time.** RFC 3339 instants in UTC with an explicit offset. Durations in hours are integers. No local times, no date-only fields where an instant is meant.
- **Sizes.** Byte counts are integers (`int64`). A "size" field always means the payload size in bytes, never the row size.
- **Payloads.** Base64 strings, exact bytes as published. Marites applies no transformation (DEC-006).
- **Ratios.** `deadLetterRate` is a fraction between 0 and 1, not a percentage. The client formats it.
- **Counts.** Integers (`int64`). Where `countingModel` is not `exact`, the client renders `≈` — the number is unchanged, its interpretation is qualified.
- **Nullability.** Absent and null are distinct. A field that has never been set is absent; a field explicitly cleared is null.
- **Unknown fields.** Clients ignore unknown fields and never fail on their presence.

## 3. Command conventions

A **command** changes state. A **query** does not. Every command is identified as such in [DOC-API-002](api-catalog.md).

1. **Idempotency.** Every command that can be retried carries an `Idempotency-Key` header. Repeating it returns the recorded outcome with `outcome: AlreadyApplied`. Reusing the same key with different parameters returns `ERR-CMN-005` and creates no second effect.
2. **Revision guard.** Commands that modify an existing configuration record — broker profile, capture policy — carry `If-Match` with the `revision` the caller last read. A stale revision returns `ERR-CMN-006`. The client must re-read and re-apply, never blind-retry.
3. **Typed confirmation.** Destructive operations (`discard`, `purge`) require a `confirmation` field containing the queue name as typed by the operator. A mismatch returns `ERR-OPR-001` and performs nothing. When the target environment is `prod`, `environmentConfirmation` is also required — for `replay` and `publish` too, where it is the only typed confirmation they carry.
4. **Dry run first.** `replay`, `discard` and `purge` accept `dryRun`. The client must call with `dryRun: true` and display the result before executing. The API does not enforce the ordering — it cannot know what was displayed — but the conformance suite asserts the dry-run response shape.
5. **Capability check.** A command whose provider cannot perform it returns `ERR-CAP-001`, never a silent no-op. The client should not have offered the control; the error exists because clients can be wrong.
6. **Rate limiting.** Mutating operations are rate limited per tenant. Exceeding it returns `ERR-CMN-004` with `Retry-After`.

## 4. Outcome classes and HTTP

Every response maps to exactly one outcome class. The class is returned in the problem body's `outcome` field so clients do not infer it from the status code alone.

| Outcome | Meaning | HTTP |
|---|---|---|
| `Applied` | The command took effect | 200 or 201 |
| `AlreadyApplied` | The recorded outcome of an earlier identical command | 200 |
| `Rejected` | A rule refused it, with a specific code | 400, 422 or 429 |
| `Denied` | Authentication, authorization or tenant scope refused it | 401, 403 or 404 |
| `Conflict` | Stale revision, duplicate name, or competing claim | 409 |
| `Unresolved` | The effect may or may not have happened; reconcile through the same operation | 202 |
| `Unavailable` | The service or a dependency cannot serve now | 503 |

**`Unresolved` matters most.** A replay that fails mid-flight is not a failure — some messages were published. Reporting it as `503` would invite a retry that publishes them twice. It is `202`, and the client polls `/operations/{id}`.

**`Denied` returns 404 rather than 403** where the caller must not learn whether the resource exists. For Marites this applies across tenant boundaries only; within a tenant, 403 is correct because the caller already knows the queue exists.

## 5. Errors

Every error body is a `Problem`:

```json
{
  "type": "https://marites.dev/errors/ERR-CAP-001",
  "title": "Capability unavailable",
  "status": 422,
  "code": "ERR-CAP-001",
  "outcome": "Rejected",
  "retryable": false,
  "detail": "The connected provider does not support replay.",
  "remedy": "Remove the replay control for this provider, or connect a provider whose replayModel is not 'none'."
}
```

- `code` is stable and machine-readable. Clients branch on it, never on `detail` text.
- `remedy` is present whenever the operator can fix the cause. It is written for an operator, not a developer.
- `retryable` states whether the same request may be repeated unchanged. It is `false` for every `Rejected` outcome.

## 6. Paging

Cursor paging, never offset.

| Parameter | Rule |
|---|---|
| `limit` | 1 to 200, default 50 |
| `cursor` | Opaque string, returned as `nextCursor`; absent on the last page |
| Ordering | Stable and documented per endpoint — `capturedAt` descending for messages, `createdAt` descending for audit |

A cursor is a position in one stable ordering. Changing filters while holding a cursor is undefined; clients discard the cursor when filters change.

## 7. Versioning and compatibility

- The major version is the path segment: `/api/v1`.
- **Compatible:** adding an optional request field, adding a response field, adding an enum value the client must tolerate.
- **Breaking:** removing or renaming a field, tightening a validation rule, changing a field's meaning. Each requires `/api/v2`, and v1 keeps working until it is formally retired.
- Enum values are **open** for `DeathReason` and `CredentialKind` — new provider values may appear. Clients must not crash on an unrecognised value; they display the raw string. Closed enums (`ProviderKind`, `SupportTier`, `PayloadPolicy`) change only with a major version.

## 8. Security schemes

One scheme: `bearerAuth`, an Entra ID access token. Roles arrive as App Roles in the `roles` claim and map to capability policies ([DOC-UX-001 §2](../experience/dashboard-and-operations.md)).

- The contract names the capability each operation requires.
- Token format, key handling and transport protection belong to deployment, not to this document.
- **Credentials are write-only.** No response carries a broker secret in any form, including masked. A response that echoed one would be a defect regardless of the schema.

## 9. What this document does not decide

Implementation language, framework, database, deployment topology, or the internal structure of the service. Those are owned by [DOC-ARCH-001](../architecture/architecture-overview.md). This document constrains only what crosses the HTTP boundary.

## 10. Document control

Revision 0.1 establishes the conventions. `data-and-contracts/openapi/openapi.yaml` was written before this document existed and has been reconciled to it; future contract changes follow these rules first and amend the contract second.
