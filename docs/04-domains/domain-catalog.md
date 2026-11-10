# Documentation area catalog

Version 0.1 | November 2026 | Status: Drafted; groups existing records by responsibility and adds no policy.

## Purpose and boundary

These five areas organise the documentation by responsibility. They are not five services, final aggregates or deployment units. The deployable shape is fixed separately: one image with three entrypoints, split by capability into projects ([architecture overview §6, §9](../05-architecture/architecture-overview.md)). An area may span several projects, and a project may serve several areas.

## Current area coverage

| Documentation area | Responsibilities | Records | Boundary and handoff | Existing sources | Remaining work |
| --- | --- | --- | --- | --- | --- |
| Broker connections and providers | Register, test and remove broker profiles; preflight readiness; the provider capability record; credentials | `broker_profile`, `ProviderCapabilities`, credential payload | Never provisions or alters the adopter's topology (ADR-001); creates only `marites.`-prefixed objects. Hands queue identity to capture | [Deployment §1–§4](../11-operations/deployment-and-onboarding.md); [architecture §6](../05-architecture/architecture-overview.md); [vocabulary §12–§14](../14-reference/vocabulary.md); [onboarding lifecycle](../03-workflows/onboarding-lifecycle.md); FR-012 to FR-014 | DEC-008 premise unconfirmed; no Service Bus or Pub/Sub platform configured |
| Capture and counting | Shadow bindings, normalisation, batching, exact rollups, row retention, capture health | `queue_ref`, `capture_rule`, `message_capture`, `message_rollup`, `capture_dedupe` | Counting is separate from storing (ADR-007). A dead letter is observed twice and never summed. Capture is forward-only (C-002). Supplies counts to reporting | [Architecture §2–§4, §7](../05-architecture/architecture-overview.md); [data model §3–§4](../07-data-and-contracts/data-model.md); [capture lifecycle](../03-workflows/capture-lifecycle.md); ADR-002, 007, 011, 012, 019; FR-002 to FR-005, FR-015, FR-018, FR-022 | Throughput unmeasured; backfill design unspecified; compaction idempotency unspecified (RSK-010) |
| Dead-letter recovery | Peek, discard, purge, replay and publish behind one guard chain | `replay_job`, `operation_audit`, confirm tokens (stateless) | Live operations need no capture. Replay and publish are the highest-risk surface. No scheduled destructive operation, ever (NFR-004) | [Experience §3](../08-experience/dashboard-and-operations.md); [recovery lifecycle](../03-workflows/recovery-lifecycle.md); [API catalog §6](../07-data-and-contracts/api-catalog.md); FR-008 to FR-011 | Quorum-queue peek effect unverified (TC-010); per-provider semantics in vocabulary §14 and adapter design |
| Reporting and metrics | Four dashboard tiles, granularity availability, the Viewer reporting surface | `message_rollup` tiers | Reads rollups only, never `message_capture`. `published` and `dead_lettered` are separate series | [Experience §1](../08-experience/dashboard-and-operations.md); [data model §3.1.2](../07-data-and-contracts/data-model.md); [API catalog §5](../07-data-and-contracts/api-catalog.md); FR-004, FR-005, FR-020, FR-021 | Metric endpoints deferred to M2; visual design unwritten |
| Access, roles and audit | Identity mapping, roles to capabilities, break-glass, append-only audit | `app_user`, `tenant`, `operation_audit` | Entra ID owns sessions; Marites stores no tokens. A tenant is a team in one organisation (ADR-014) | [Architecture §8](../05-architecture/architecture-overview.md); [experience §2](../08-experience/dashboard-and-operations.md); [vocabulary §2](../14-reference/vocabulary.md); ADR-008; FR-016, FR-017 | Role-assignment flow; break-glass procedure; tenancy isolation case planned (TC-022) |

## Standard for each future area specification

1. Purpose, confirmed scope and exclusions.
2. Actors, actions and ownership boundaries, separating accepted policy from proposed design.
3. Records and vocabulary, citing [VOCABULARY](../14-reference/vocabulary.md) rather than restating it.
4. Rules, state meanings, workflows and deadlines.
5. Incoming and outgoing facts, with the authoritative source of each.
6. Authorisation, exposure, audit and consistency expectations.
7. Screens and verification links where applicable.
