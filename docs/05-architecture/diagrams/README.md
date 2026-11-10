# Architecture diagrams

The rendered figures for [architecture overview](../architecture-overview.md). Each `.png` is built from the `.json` beside it — the spec is the source, the picture is output, and neither is edited by hand.

| Spec | Figure |
|---|---|
| `01-architecture-baseline.architecture.json` | §1, the runtime topology |

## Rebuilding

```bash
python build.py 01-architecture-baseline.architecture.json
```

Needs the [Archify](https://github.com/tt-a1i/archify) checkout (set `ARCHIFY` to its `archify/` directory) and Chrome. `build.py` renders through the Archify CLI, isolates the diagram from the page chrome, captures it at 2x, and trims the margin.

**Why PNG and not the HTML Archify emits.** Its output is a self-contained page with interactive SVG and ~300 KB of inlined fonts — right for a browser, wrong for a document. GitHub will not render it inline, and the SVG alone loses the CSS cascade it depends on, because the theme variables live on `<html>`.

**Why the JSON is committed.** A diagram nobody can edit is a picture of a decision nobody can check. The spec is small, reviewable, and the validator reports overlapping labels with exact coordinates, so a change to the architecture is a diff rather than a redraw.

## What is deliberately not a diagram

The other fenced blocks in the overview are a directory tree (`src/`), two type definitions (`ProviderCapabilities`, `DeathInfo`), a slice listing and a one-line read path. They are code and lists, and drawing them would make them harder to read, not easier.
