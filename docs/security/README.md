# Security and access

Version 0.2 | 09 November 2026 | Status: Partial; threat model drafted, no control implemented or tested.

Apply credential protection, payload responsibility, role boundaries and operation safeguards to every operation Marites performs.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Threat model](threat-model.md) | Assets, six trust boundaries, 26 threats (THR-001 to THR-026) with controls and residual risk, and the six gaps that remain |
| [Credential kinds](../reference/vocabulary.md) (§13) and [ADR-018](../architecture/decisions.md) | Eight credential shapes in use, ambient credentials first-class, write-only credentials, encrypted per row |
| [NFR-003 and NFR-004](../business/requirements.md) | Credentials never logged or returned; no destructive operation without explicit human confirmation |
| [Payload responsibility](../operations/deployment-and-onboarding.md) (§1.7) and DEC-006 | Bodies stored exactly as published; no masking; `payload_policy = none` is the only way to avoid storing them |
| [Role and capability matrix](../experience/dashboard-and-operations.md) (§2) | Three roles mapped to eleven capability policies; Viewer has no message-level access (DEC-002) |
| [Identity and break-glass](../architecture/architecture-overview.md) (§8) | Entra ID App Roles; a local admin disabled by default and audited on every use |
| [Operation safety model](../experience/dashboard-and-operations.md) (§3) | Guard chain: authorise, validate, dry run, confirm token, rate limit, execute, audit |
| [Least-privilege broker user](../operations/deployment-and-onboarding.md) (§1.2, §4.1) | The exact grants requested and every object Marites creates in a broker |
| [Provider prerequisites](../operations/provider-prerequisites.md) | The equivalent least-privilege sets for Service Bus and Pub/Sub, with the commands that verify them |
| [Contract security](../data-and-contracts/contract-conventions.md) (§3, §8) | Idempotency, revision guard, typed confirmation, environment confirmation, bearer scheme |

## The three things a reviewer should read first

1. **The shadow-queue grant (§3.5 of the threat model).** Marites asks for write rights on the adopter's exchanges. That is the largest privilege in the design, and the section states plainly what it allows and what it does not.
2. **Payload responsibility (deployment §1.7).** Bodies are stored as published. An adopter whose policy requires redaction inside the tool is not a customer for that queue, and saying so up front is the control.
3. **What is accepted rather than mitigated.** An Admin is trusted; the broker management UI is outside the boundary; preflight can be used to probe internal hosts by an Admin. Each is a decision, not an oversight.

## Work still needed

- **Key-rotation procedure** for the key-encryption key. A suspected compromise has no documented response.
- **TLS parameter requirements.** Minimum version and cipher suites are unstated; deployment says "TLS" and stops.
- **Rate-limit values.** The mechanism is specified, the numbers are not.
- **Object-store spillover lifecycle.** Row deletion does not yet state what happens to spilled payload objects.
- **No independent review and no penetration test.** This model is a self-assessment against specifications that describe no running software.

Use the [documentation structure](../project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
