# Reference

Version 0.4 | 09 November 2026 | Status: shared documentation section and coverage index.

Canonical vocabulary: every enumerated value, defined once.

## Current Marites records

| Record | Shared coverage |
| --- | --- |
| [Vocabulary](vocabulary.md) | Controlled. Source projects, roles and capabilities, metrics, dead-letter reasons, payload policy, replay and preflight state, environment tags, identifiers, queue identity, terms, provider kinds, credential kinds, operation support, capture state |

## Why this file is not optional

Four artefacts consume these values: the C# implementation, the OpenAPI contract, the database schema and the React UI. Each is written at a different time and none reads the others' source. That is the condition under which one value gets spelled two ways. If something needs an enumerated value it links here; restating it is a defect.

The 09 November 2026 pass found one that had escaped: `captureState` was defined only inside the contract's `QueueSummary` and used in three documents. It now has a section (§15), which is what the rule above is for — an enum with no owner is an enum that gets re-derived.

## Reserved values

`replay_model` carries `redrive` and `offset_reset`, `discard_model` carries `truncate`, and the credential table carries four AWS and SASL kinds. None is used by a v1 provider. They are kept so that adding a platform later does not require inventing vocabulary under delivery pressure.

## Where Marites differs

This section holds the controlled vocabulary, which plays the part a data dictionary plays elsewhere. The data dictionary itself lives in [data-and-contracts](../data-and-contracts/data-dictionary.md) and owns column-level types; it does not define enumerated values, and links here for them. Marites adopts no external standard by reference.

## Work still needed

- **Wire protocol reference.** Provider connection details live in [operations](../operations/deployment-and-onboarding.md) §4 and [provider prerequisites](../operations/provider-prerequisites.md).
- **Localisation.** All user-facing strings are English; a strings section would be added here if that changes.

Use the [documentation structure](../project/documentation-structure.md) for the common writing standard, current-versus-planned status and the rules for index-only sections. Each linked document is maintained in its section as the authoritative record for that topic.
