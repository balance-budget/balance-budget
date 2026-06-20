# 0031 — Light theme, token polarity, and theme-preference plumbing

## Status

Accepted (2026-06-20)

## Context

Balance shipped dark-only. Every color token in `index.css`'s `@theme` block
holds a dark value, `html { color-scheme: dark }` is hardcoded, and the
surface/`*-soft` tokens are alpha-on-dark overlays (`rgba(255,255,255,0.04)`,
`rgba(0,0,0,0.3)`, `rgba(255,93,93,0.14)`). Charts and the whole UI already
read these as CSS variables, so the rendered palette is centralized — but it
is dark by construction.

We want a first-class light theme that *matches* the dark one (not an
algorithmic inversion), a tri-state preference (`auto`/`light`/`dark`) that
persists per user and follows them across devices, a quick toggle in the
topbar, and the full choice in Settings. The user is on a shared ledger
(ADR-0015), so the preference is per-user, not per-ledger.

## Decision

**Persistence is hybrid.** `BalanceUser.Theme` (nullable, opaque string,
`MaximumLength(16)`) is the durable source of truth, following the
display-preference precedent of ADR-0022 — the backend never interprets the
value; the SPA owns the `auto`/`light`/`dark` vocabulary and the `auto`
default (`null` = default). The value is mirrored into `localStorage` and
applied by a synchronous inline boot script in `index.html`'s head (no CSP
blocks it) **before React paints**, eliminating the flash-of-wrong-theme that
a purely server-loaded preference would cause. A `ThemeProvider` context is
the single owner: `setPreference` updates the DOM, `localStorage`, and fires
`useUpdatePreferences` together; both the topbar and Settings drive it. On
`useCurrentUser` load the server value reconciles and **server wins**.

**`auto` follows the OS.** It resolves via `prefers-color-scheme` at boot, and
a `matchMedia` listener (inside `ThemeProvider`) re-resolves live — but only
while the active preference is `auto`. Explicit `light`/`dark` ignore the OS.

**Mechanism is Tailwind-standard.** `@custom-variant dark (&:where(.dark,
.dark *))` plus a `.dark` class on `<html>`. The DOM always carries the
*resolved* theme (`light` or `dark`), never the `auto` preference; `auto` is
resolved before being written. `color-scheme` and a `<meta name="theme-color">`
(updated by `ThemeProvider`) are class-driven.

**Token polarity flips to light-base.** `@theme` becomes the light palette and
today's dark values move under `.dark`. This is the idiomatic Tailwind model
(base = light, `dark:` = override) and makes `dark:` utilities behave
conventionally, at the cost of relocating the entire existing dark palette.

**Light palette is hand-tuned and brand-faithful**, targeting WCAG AA (text)
and ~3:1 (UI). Neutrals are authored from Tailwind's built-in warm-neutral
color variables. Core brand/semantic/chart hues stay constant across themes;
only hues used as text/border that fail AA on white are darkened in light. The
`*-soft` and overlay tokens are re-expressed as `color-mix(in oklab, <hue>,
var(--color-bg-0) …%)` against the surface tokens, so a single definition
auto-derives correctly in both themes instead of carrying two hand-tuned alpha
values.

**Topbar is a 2-state toggle.** A sun/moon button sets an explicit
`light`/`dark` (leaving `auto` behind); `auto` is reachable only from the
Settings `Select`. The icon shows the target theme with an action `aria-label`.

## Considered alternatives

- **Local-only preference** — simpler, no backend change, but doesn't follow
  the user across devices. Rejected for the hybrid above.
- **Server-only preference** — consistent with other prefs but paints the
  default theme first and snaps, causing a flash. Rejected; hence the
  localStorage mirror + inline boot script.
- **`data-theme` attribute instead of `.dark` class** — reads cleanly but is
  off the Tailwind rails and forgoes `dark:` utilities. Rejected for the
  class convention.
- **Keep dark as the `@theme` base, light as override** — lower-risk (no
  palette relocation) but inverts Tailwind's base-is-light assumption.
  Rejected in favor of the orthodox light-base flip.
- **Algorithmic (OKLCH L-flip) inversion** — fast but produces muddy,
  low-contrast light themes. Rejected for hand-tuning.
- **Topbar cycling all three states / topbar popover** — honest about the
  tri-state model but fiddly (icon ambiguity) or heavier. Rejected for the
  predictable 2-state toggle.

## Consequences

- Two EF migrations (SQLite + Postgres) add the nullable `Theme` column.
- `index.css` is restructured: light tokens in `@theme`, a `.dark` override
  block, `@custom-variant dark`, class-driven `color-scheme`, and `color-mix`
  derivations for `*-soft`/overlays/gradient.
- The lone non-token color offender (`bg-white/[0.03]` in `Sidebar.tsx`) moves
  to a surface token; `text-white`/`bg-white` on brand/semantic fills stay
  (white-on-fill is legitimately theme-constant).
- A rare one-time flash can occur only when a user's devices disagree and the
  server value overrides the local mirror after load — accepted.
