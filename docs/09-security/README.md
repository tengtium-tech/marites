# Security and access

Version 0.3 | November 2026 | Status: Approved — Product owner, 2026-11-11

Apply credential protection, payload responsibility, role boundaries and operation safeguards to every operation Marites performs.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Threat model](threat-model.md) | Assets, six trust boundaries, 29 threats (THR-001 to THR-029) with controls, and the seven residual risks that no control removes |
| [Credential kinds](../14-reference/vocabulary.md) (§13) and [ADR-018](../05-architecture/decisions.md) | Eight credential shapes in use, ambient credentials first-class, write-only credentials, encrypted per row |
| [NFR-003 and NFR-004](../02-business/requirements.md) | Credentials never logged or returned; no destructive operation without explicit human confirmation |
| [Payload responsibility](../11-operations/deployment-and-onboarding.md) (§1.7) and DEC-006 | Bodies stored exactly as published; no masking; `payload_policy = none` is the only way to avoid storing them |
| [Role and capability matrix](../08-experience/dashboard-and-operations.md) (§2) | Three roles mapped to eleven capability policies; Viewer has no message-level access (DEC-002) |
| [Identity and break-glass](../05-architecture/architecture-overview.md) (§8) | Entra ID App Roles or Google sign-in with an explicit `(issuer, subject)` mapping; a Marites session cookie in Google mode; a local admin disabled by default and audited on every use |
| [Operation safety model](../08-experience/dashboard-and-operations.md) (§3) | Guard chain: authorise, validate, dry run, confirm token, rate limit, execute, audit |
| [Least-privilege broker user](../11-operations/deployment-and-onboarding.md) (§1.2, §4.1) | The exact grants requested and every object Marites creates in a broker |
| [Provider prerequisites](../11-operations/provider-prerequisites.md) | The equivalent least-privilege sets for Service Bus and Pub/Sub, with the commands that verify them |
| [Contract security](../07-data-and-contracts/contract-conventions.md) (§3, §8) | Idempotency, revision guard, typed confirmation, environment confirmation, bearer scheme |

## The three things a reviewer should read first

1. **The shadow-queue grant (§3.5 of the threat model).** Marites asks for write rights on the adopter's exchanges. That is the largest privilege in the design, and the section states plainly what it allows and what it does not.
2. **Payload responsibility (deployment §1.7).** Bodies are stored as published. An adopter whose policy requires redaction inside the tool is not a customer for that queue, and saying so up front is the control.
3. **What is accepted rather than mitigated.** An Admin is trusted; the broker management UI is outside the boundary; preflight can be used to probe internal hosts by an Admin. Each is a decision, not an oversight.

Use the [documentation structure](../01-project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
