---
document_id: DOC-UX-002
title: Accessibility Requirements
product: Marites
version: 0.1
date: 2026-11
status: Draft — requirements only; nothing built or audited
---

# DOC-UX-002 — Accessibility Requirements

**Source:** [NFR-008](../02-business/requirements.md); [dashboard and operations](dashboard-and-operations.md); [wireframes and navigation](wireframes-and-navigation.md). Owning document for accessibility.

Accessibility is a requirement here for an operational reason, not a compliance one: **the operations console is used during an incident**, often by someone on a call, in a hurry, with one hand on a laptop and a phone against their ear. Keyboard-only operation is not an accommodation for that person; it is the fastest path for everyone. WCAG 2.2 level AA is the floor because it is checkable, not because a certificate is the goal.

## 1. Scope

| Surface | In scope | Why |
|---|---|---|
| Operations console | **Yes** — AA, keyboard-first | Incident use. The primary surface and the one with the tightest constraint |
| Reporting dashboard | **Yes** — AA | Read by executives, often on a large screen and often by someone who zooms |
| Setup wizard | **Yes** — AA | The first thing an adopter meets; a wizard that cannot be completed by keyboard is an install failure |
| Email or exported reports | **No** | None exist |

## 2. Requirements

Grouped by concern. Each is testable, and each names the WCAG 2.2 criterion it satisfies where one applies.

### 2.1 Keyboard

- **Every action is reachable and operable with the keyboard alone**, in a logical order, with no keyboard trap. The queue list, the message list, the operation dialog and the wizard are the four that must be proven. *(2.1.1, 2.1.2)*
- **Focus is visible at all times**, with a contrast ratio of at least 3:1 against its surroundings. A focus ring removed for aesthetics makes the console unusable by keyboard. *(2.4.7, 1.4.11)*
- **Focus moves to the dialog when it opens and returns to the invoking control when it closes.** The operation dialog is modal; a lost focus position after cancelling a replay is how someone reopens it by accident. *(2.4.3)*
- **The destructive action is never the default focus.** The dialog focuses its heading or the cancel control, never the confirm button. *(2.4.3, 3.3.4)*
- **Single-key shortcuts do not exist.** If a shortcut is added later it must be remappable or require a modifier, because a console with single-letter shortcuts collides with screen-reader navigation. *(2.1.4)*

### 2.2 Structure and semantics

- **Every screen has one `h1` and a heading hierarchy that reflects the layout**, so a screen-reader user can navigate by heading rather than by tabbing through forty rows. *(1.3.1, 2.4.6)*
- **The queue list is a real table** with column headers, not a grid of divs. Row selection announces the row's identity, not "row 3". *(1.3.1)*
- **Status is conveyed in text, not by colour or icon alone.** `live`, `stale`, `capped`, `disabled` and `unavailable` each carry a word. This is the accessibility requirement and the anti-misreading requirement at the same time — the same rule that stops a stale queue looking quiet stops it looking like a green dot. *(1.4.1, 1.4.3)*
- **Form fields have programmatic labels**, and the typed-confirmation field names the exact string expected in its label, not only in placeholder text. *(1.3.1, 3.3.2)*

### 2.3 Visual

- **Text and background meet 4.5:1; large text and UI components meet 3:1.** *(1.4.3, 1.4.11)*
- **Information is not lost when colour is unavailable.** The dead-letter rate uses a number and a label; a red/amber/green scale may accompany it, never replace it. *(1.4.1)*
- **Layout survives 200% zoom and 320 CSS pixels of width** without loss of content or function. The console is desktop-first, which is not an exemption. *(1.4.4, 1.4.10)*
- **Charts have a text alternative and a data table.** The trend sparkline and the volume chart must each be accompanied by the values in a table, because a chart is not readable by a screen reader and a caption alone is not the data. *(1.1.1)*
- **Motion is not required to understand a change.** A live-updating figure must not animate in a way that cannot be paused, and no content flashes more than three times per second. *(2.2.2, 2.3.1)*

### 2.4 Time and error

- **No session timeout shorter than the task.** A replay dialog left open while the operator checks with a colleague must not expire silently; where a token expires, the action is refused with a clear message and the form's contents are preserved. *(2.2.1)*
- **Errors identify the field and state the fix in text.** `ERR-BRK-001` must render as "your user lacks `configure` on `marites.`" rather than a code and a red border. *(3.3.1, 3.3.3)*
- **The confirmation requirement is stated before the field, not after a failed submit.** *(3.3.2)*

### 2.5 Target size

- **Every interactive control has a target of at least 24×24 CSS pixels**, and the row-level targets in the queue and message lists are the full row. *(2.5.8)*

## 3. Per-surface notes

| Surface | The constraint that matters most |
|---|---|
| Queue list | Keyboard traversal of a long list. Arrow-key navigation within the table plus a skip link to the action bar |
| Operation dialog | Focus order and the confirm affordance. The dialog must be completable without a mouse, including the typed confirmation |
| Reporting dashboard | Charts. Every chart needs a table equivalent, and the granularity caption must be in the accessible name, not only in the visual subtitle |
| Setup wizard | Step state must be announced when it changes, and a failed preflight must move focus to the first failure |

## 4. What automated checks cannot establish

An automated audit finds roughly a third of real problems. It cannot tell you whether the queue list makes sense when read aloud, whether the dry-run warning is understandable, or whether a keyboard user can complete a replay without losing their place. Those need a manual pass, and this document does not pretend otherwise.

**No assistive technology has been tested, because nothing has been built.** Every requirement above is a design constraint recorded before implementation, which is the cheapest time to record one and the only time it costs nothing to change.

## 5. Verification

| Case | Asserts |
|---|---|
| TC-011 | Both surfaces pass an automated WCAG 2.2 AA audit with zero A or AA violations, and every action in §2.1 is operable keyboard-only |

TC-011 is necessary and not sufficient. A manual screen-reader pass over the queue list and the operation dialog is a separate, unwritten check.

## 6. Gaps

| Gap | Consequence |
|---|---|
| No manual screen-reader test plan | The most common real failures are unverified |
| No colour tokens | Contrast cannot be asserted before a palette exists; the requirement is stated against a palette that is not designed |
| No reduced-motion handling | `prefers-reduced-motion` is not mentioned in the requirements; it should be, once any animation exists |
| No localisation | All strings are English. Right-to-left support is not considered, and a layout described in text is the right time to think about it |

## 7. Document control

Revision 0.1 records the accessibility requirements behind NFR-008, written November 2026. NFR-008 was added at the same time because the [experience index](README.md) had listed accessibility as a gap with no requirement behind it, which meant nothing could fail. Nothing has been built or audited.
