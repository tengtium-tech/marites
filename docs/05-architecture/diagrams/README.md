# Architecture diagrams

Version 0.1 | November 2026 | Status: Approved — Product owner, 2026-11-11.

The rendered figures for the [architecture overview](../architecture-overview.md) and [C4 model](../c4-model.md). Each `.png` is built from the `.json` beside it: the JSON is the editable source, and the PNG is its inline preview.

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

## Rebuilding previews

From the repository root:

```bash
python docs/05-architecture/diagrams/build.py docs/05-architecture/diagrams/*.architecture.json docs/05-architecture/diagrams/*.sequence.json
```

The same builder renders workflow figures from `docs/03-workflows/diagrams/`; it accepts source paths from any documentation folder. It requires the configured local diagram renderer and Chrome. The builder captures the drawing and its explanatory cards, then writes a PNG preview.

Interactive HTML views are local build products and are not committed. The source JSON files remain the editable records for each figure.

## What is deliberately not a diagram

The other fenced blocks in the overview are a directory tree (`src/`), two type definitions (`ProviderCapabilities`, `DeathInfo`), a slice listing and a one-line read path. They are code and lists, and drawing them would make them harder to read, not easier.
