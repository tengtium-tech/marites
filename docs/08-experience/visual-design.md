---
document_id: DOC-UX-004
title: Visual Design Tokens
product: Marites
version: 0.1
date: 2026-11-10
status: Draft — tokens proposed and measured; no visual design review
---

# DOC-UX-004 — Visual Design Tokens

**Source:** [accessibility](accessibility.md) NFR-008; [wireframes](wireframes-and-navigation.md). Owning document for colour, type, spacing, radius and elevation. The prototype at `prototype/` renders these tokens; where the two disagree, this document wins.

**Status of these values.** They are a **proposal**, chosen to clear WCAG 2.2 AA and measured by computing relative luminance from the HSL values below. They have not been reviewed by a designer, and they have not been checked in a browser against a real display.

## 1. Why tokens and not a stylesheet

Four surfaces consume these values — the React client, the future Go and Python clients, the exported reports, and the screenshots in this repository. A hex value repeated in four places drifts in three of them. A token is one name, one value, one place.

The values are declared as HSL triplets in `prototype/src/index.css` and consumed through Tailwind as `hsl(var(--token) / <alpha>)`, which is what makes `bg-success/10` possible: the same colour at 10% for a tinted chip, at full strength for its text.

## 2. Colour — light

| Token | HSL | Role | Contrast on card |
|---|---|---|---|
| `--background` | `220 20% 97%` | Application canvas | — |
| `--card` | `0 0% 100%` | Surfaces above the canvas | — |
| `--foreground` | `222 24% 8%` | Primary text | **18.7 : 1** |
| `--muted-foreground` | `220 9% 42%` | Secondary text, labels, captions | **5.6 : 1** |
| `--border` | `220 14% 91%` | Hairlines, table rules | — |
| `--primary` | `232 78% 53%` | The one accent: primary buttons, focus, links, `live` | **7.0 : 1** |
| `--success` | `160 76% 26%` | `live`, configured, applied | **5.7 : 1** |
| `--warning` | `32 100% 29%` | `stale`, `capped`, caution callouts | **6.2 : 1** |
| `--destructive` | `4 72% 39%` | `prod`, purge, invalid input | **7.0 : 1** |
| `--idle` | `220 9% 38%` | `disabled`, `unavailable`, neutral chips | **6.5 : 1** |

## 3. Colour — dark

| Token | HSL | Contrast on card |
|---|---|---|
| `--background` | `220 22% 5%` | — |
| `--card` | `220 20% 8%` | — |
| `--foreground` | `220 20% 96%` | **16.9 : 1** |
| `--muted-foreground` | `220 10% 66%` | **7.6 : 1** |
| `--border` | `220 16% 17%` | — |
| `--primary` | `230 100% 77%` | **7.4 : 1** |
| `--success` | `160 62% 56%` | **10.0 : 1** |
| `--warning` | `38 85% 64%` | **10.4 : 1** |
| `--destructive` | `5 100% 72%` | **7.4 : 1** |
| `--idle` | `220 10% 64%` | **7.1 : 1** |

**Dark is not an inversion.** The accent lightens from 53% to 77% lightness and the status hues desaturate, because a colour that reads as strong on white reads as muddy on near-black. Both palettes were chosen independently against the same contrast floor.

## 4. Status colour is never the only signal

Every status token is paired with a **word** and a shape:

| State | Token | Word | Shape |
|---|---|---|---|
| `live` | `--success` | live | filled dot |
| `stale` | `--warning` | stale | filled dot |
| `capped` | `--warning` | capped | filled dot |
| `disabled` | `--idle` | disabled | hollow dot |
| `unavailable` | `--idle` | unavailable | hollow dot |

A colour-blind operator, a printed screenshot and a screen reader all receive the same information. This is the requirement in [accessibility §2.2](accessibility.md) and it is also the anti-misreading rule: a green dot with no word is a claim nobody can check.

## 5. Type

One family, one scale. `font-feature-settings: "cv11", "ss01"` on the body, and **tabular numerals** (`.num`) on every figure — a column of proportional digits does not line up, and these screens are columns of digits.

| Role | Size / line height | Weight | Tracking |
|---|---|---|---|
| Page title | 22 / 1.2 | 600 | −0.025em |
| Section title | 12 uppercase | 600 | +0.08em |
| Metric | 24–26 / 1.1 | 600 | −0.03em |
| Body | 13.5 / 1.45 | 400 | −0.005em |
| Table cell | 13.5 | 400 | 0 |
| Label | 11.5 | 500 | 0 |
| Caption | 11.5–12 | 400 | 0 |
| Mono (identifiers, payloads) | 12–12.5 | 400 | 0 |

Negative tracking on large text, positive on small caps — the opposite of a single global letter-spacing.

## 6. Spacing, radius, elevation

| Token | Value | Use |
|---|---|---|
| Base unit | 4px | Every gap is a multiple |
| Control height | 32 / 36 / 40px | `sm`, default, `lg` |
| Radius | `--radius: 0.625rem` | Cards 10px, controls 7px, dialogs 16px |
| Shadow — card | `0 1px 2px` + `0 10px 30px -18px` | Two layers: a hairline and a soft ambient |
| Shadow — dialog | `0 2px 6px` + `0 40px 90px -40px` | Deeper, because it must sit above a scrim |

Elevation is used sparingly: a border does the separating work, and the shadow only suggests depth. A dashboard where every card floats is a dashboard where nothing is more important than anything else.

## 7. Component inventory

| Component | Variants | States that must exist |
|---|---|---|
| Button | default, outline, ghost, destructive | hover, focus, disabled, loading |
| Badge | success, warning, destructive, idle, secondary | — (status chips) |
| Card | — | — |
| Table | — | row hover, row selected |
| Dialog | — | open, closing, scrollable body, pinned footer |
| Input | — | rest, focus, invalid, disabled |
| Checkbox | — | unchecked, checked, indeterminate, disabled |
| Status chip | five capture states | always word + dot |

**Disabled is a state, not a hiding place.** Where a role or provider cannot perform an action, the control is **absent** — see [DOC-UX-001 §2](dashboard-and-operations.md). Disabled is reserved for "not yet": nothing selected, a body that does not parse, a confirmation not typed.

## 8. What this document does not do

- **No brand.** There is a mark — an ear on an indigo tile — and nothing beyond it: no logo variants, no spacing rules around it, no monochrome version.
- **No icon set beyond lucide.** Icons are 16px, 1.8–2.2px stroke, and chosen for meaning rather than decoration.
- **No motion specification.** Transitions are 140–220ms with a standard ease; nothing states what may animate, and `prefers-reduced-motion` is not handled.
- **No measured audit.** The ratios above are computed from the token values. No browser audit, no screen-reader pass, no contrast check against a real display.

## 9. Document control

Revision 0.1 records the token set the prototype was built on, written 10 November 2026 after the UI work established values that had no owning document. Contrast ratios are computed from the HSL triplets, not measured in a browser. Every value is replaceable; the names are the contract.
