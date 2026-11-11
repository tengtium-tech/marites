# Architecture diagrams

The rendered figures for the [architecture overview](../architecture-overview.md) and [C4 model](../c4-model.md). Each `.png` is built from the `.json` beside it — the spec is the source, the picture is output, and neither is edited by hand.

| Source JSON | Figure |
|---|---|
| `01-architecture-baseline.architecture.json` | Overview §1, the runtime topology and the worker decomposition |
| `c4-context.architecture.json` | C4 §1, system context |
| `c4-containers.architecture.json` | C4 §2, containers and the two stores |
| `c4-capture-components.architecture.json` | C4 §3, capture worker components |

## Rebuilding

From the repository root:

```bash
python docs/05-architecture/diagrams/build.py docs/05-architecture/diagrams/*.architecture.json
```

The same builder renders the workflow figures from `docs/03-workflows/diagrams/`; it takes any list of source paths.

It needs the [Archify](https://github.com/tt-a1i/archify) checkout (set `ARCHIFY` to its directory) and Chrome. It renders each diagram, isolates the drawing and its info cards from the page chrome, captures at 2x, and trims the margin.

The interactive HTML view is a local build product and is not committed. Render one on demand:

```bash
node <archify>/bin/archify.mjs render architecture docs/05-architecture/diagrams/c4-context.architecture.json c4-context.html --quality showcase
```

**Why the PNG and not the HTML Archify emits.** That output is a self-contained page with an interactive SVG and ~300 KB of inlined fonts — right for a browser, wrong for a document: GitHub will not render it inline, and the SVG alone loses the CSS cascade it depends on, because the theme variables live on `<html>`. Fourteen pages is also 11 MB of regenerable output in a documentation repository.

**Why the cards are captured too.** Archify puts the rules an arrow only points at — "a partial replay is 202 Unresolved: poll and reconcile" — in cards beside the canvas. A figure that drops them keeps the shape and loses the content, so the builder captures them under the diagram.

**Why the JSON is committed.** A diagram nobody can edit is a picture of a decision nobody can check. The spec is small, reviewable, and the validator reports overlapping labels with exact coordinates, so a change to the architecture is a diff rather than a redraw.

## What is deliberately not a diagram

The other fenced blocks in the overview are a directory tree (`src/`), two type definitions (`ProviderCapabilities`, `DeathInfo`), a slice listing and a one-line read path. They are code and lists, and drawing them would make them harder to read, not easier.
