# 0024 — React Aria Components for the SPA component layer

## Status

Accepted (2026-06-07)

## Context

The SPA's interactive components are all hand-rolled: a custom `Combobox`
(grouping, portalled listbox, sentinel rows, its own state module + tests), a
`DateField` built on a hidden native `<input type="date">` + `showPicker()`,
`Modal` on the native `<dialog>` element, a context-based `Toast` system, and
inline native inputs/selects/checkboxes/radios styled by copy-pasted Tailwind
class strings. This has produced recurring maintenance (e.g. the account
picker rendering behind modals), inconsistent field chrome across forms, and
accessibility coverage that depends on per-component diligence.

A component-library decision was recorded as ADR-0022 (shadcn/ui on Base UI
primitives, `56b956c`) and reverted the same day (`62eaad0`). The post-mortem:

- **shadcn/ui vendors generated component source into the repo** — hundreds of
  lines of copy-pasted TSX per component, against the goal of keeping the repo
  lean.
- **Base UI lacks the advanced components we actually need**, a date range
  picker foremost.
- What we want is **headless primitives, bring-our-own Tailwind styling**, but
  *including* the advanced widgets (date range picker, currency number field,
  command palette).

### Considered alternatives

1. **shadcn/ui on Base UI** — rejected per the revert above (vendored code,
   missing DateRangePicker).
2. **Base UI / Radix directly** — headless and dependency-shaped, but neither
   ships date/time components; we would hand-roll the hardest widgets anyway.
3. **react-aria hooks only** (`useComboBox`, `useDatePicker`, …) — maximum
   control but re-owns all composition logic that react-aria-components
   already ships; roughly the current maintenance burden with better
   foundations.
4. **Keep hand-rolling** — status quo; every new widget restarts the
   accessibility and overlay-positioning learning curve.

## Decision

Adopt **react-aria-components** (RAC) as the headless component layer, styled
with Balance's existing Tailwind v4 `@theme` tokens. Concretely:

- A `src/components/ui/` kit wraps each RAC component (`TextField`,
  `SearchField`, `NumberField`, `DatePicker`, `DateRangePicker`, `ComboBox`,
  `Select`, `Checkbox`, `RadioGroup`, `TagGroup`, `ToggleButtonGroup`
  (date-range presets), `Button`, `Modal`/`Dialog`, `Toast`, `FileTrigger`,
  `Autocomplete`, `Breadcrumbs`, …) once, baking in
  Balance field chrome via a shared style helper; screens import only from
  `ui/`. State styling uses the official `tailwindcss-react-aria-components`
  plugin (`@plugin` in `index.css`).
- **Dates:** `@internationalized/date` replaces the hand-rolled ISO helpers
  (`todayIso`, `isValidIsoDate`, `parseIsoDate`). ISO `yyyy-MM-dd` strings
  stay canonical in app state, route search params, and the wire (`DateOnly`);
  `CalendarDate` is confined inside `ui/` date components. Date *display*
  becomes locale-aware via RAC's defaults, superseding the forced
  `yyyy-MM-dd` rendering of `87f210c`.
- **Amounts:** `NumberField` with
  `formatOptions: { style: 'currency', currency: <account currency> }`; the
  field holds major-unit decimals and converts to minor units at the API
  boundary per ADR-0002.
- **Routing:** TanStack Router's `<Link>` remains the navigation primitive
  (compile-time typed routes, per ADR-0005). RAC components navigate through
  callbacks (`onAction` → `router.navigate`); if an `href`-bearing RAC
  component is ever needed, RAC `Link`'s `render` prop delegates to TanStack's
  `<Link>`. No RAC `RouterProvider` is wired.
- **Migration shape:** big-bang — all components with a RAC counterpart
  convert in one migration; no parity spike, no transitional coexistence.
  Row-selection lists (inbox/register div-grids with shift-click ranges) are
  explicitly **out of scope**; converting them to RAC `GridList`/`Table` is a
  follow-up slice. `Pagination` and purely presentational components stay
  custom.
- **Testing:** `@react-aria/test-utils` pattern testers cover app-specific
  behavior (AccountSelect filtering/sentinels/hierarchy, minor-units
  conversion, preset↔custom range interplay, server-error surfacing) — RAC's
  own keyboard/ARIA machinery is not re-tested.

## Consequences

- New dependencies: `react-aria-components`, `@internationalized/date`,
  `tailwindcss-react-aria-components`, and `@react-aria/test-utils` (dev).
  Zero vendored component source.
- RAC's Toast is currently exported under `UNSTABLE_` prefixes; the unstable
  imports are confined to `ui/Toast.tsx` so stabilization is a one-file
  rename. Toast auto-dismiss moves from 3 s to 5 s per RAC's accessibility
  guidance.
- Input chrome consistency is enforced structurally (one kit) instead of by
  copy-paste discipline.
- Dates and numbers render per browser locale (a Dutch browser shows
  `dd-mm-jjjj` segments and `1.234,56`); the app stops pretending everyone
  reads ISO.
- Overlay layering moves to RAC's portalled `Popover`/`Modal`, retiring the
  z-index bug class. The intentional no-click-outside-dismiss behavior of
  the current `Modal` is preserved (RAC's `isDismissable` default).
- `Combobox.tsx`, `combobox.state.ts(+test)`, `DateField.tsx`,
  `DateInput.tsx`, `Modal.tsx`, `Toast.tsx`, `SearchInput.tsx`, the
  `Launcher` listbox internals, and the ISO date helpers are deleted rather
  than maintained.

## Amendment — field chrome and type scale (2026-06-07)

The first pass at the `ui/` kit still let sizing drift: controls derived
their height from `padding + font-size` and disagreed (`py-2 text-14`
text fields vs `py-[5px] text-13` date pickers vs a `py-1.5` search box with
its own focus ring), and the Tailwind theme carried a bespoke pixel type
scale (`--text-11` … `--text-56`). Three decisions tighten this:

- **One control size, fixed height.** Every form control (and `Button`) is
  exactly `h-9 px-3 text-sm`, composed from `inputStyles`/`groupStyles` in
  `ui/styles.ts`. Height is explicit, never padding-derived, so text fields,
  date pickers, comboboxes and search boxes align on a row by construction —
  even when inner fonts differ (`font-mono` amounts). The `fieldSize`
  (`md`/`sm`) variant system is deleted; dense grids (advanced journal lines)
  use the same `h-9`. Rationale: two sizes existed to buy density that wasn't
  worth the drift; one size makes misalignment unrepresentable.
- **One focus treatment.** Fields focus by border swap
  (`focus-within:border-border-strong`); the brand-primary focus ring is
  reserved for buttons/toggles. `SearchField`'s one-off ring + borderless
  chrome was folded into the shared group chrome.
- **Native Tailwind theme.** The custom `--text-*` pixel variables are
  deleted; the SPA uses Tailwind's default scale (`text-xs` … `text-6xl`).
  Mapping applied: 11/12 px → `text-xs`, 13/14 px → `text-sm`, 16 → `base`,
  18 → `lg`, 22 → `xl`, 28 → `3xl`, 44 → `5xl`. The 13 px compact size — the
  main casualty, with no default equivalent — was deliberately given up: form
  controls read fine at 14 px, and staying on stock tokens beats maintaining
  a parallel scale. The same sweep removed the remaining redundant tokens:
  `--spacing: 4px` (identical to the default `0.25rem`), the tracking
  overrides, the custom radius scale (4/8/32 px snap exactly onto default
  `rounded-sm`/`lg`/`4xl`; the 14 px panel radius snaps to `rounded-xl`,
  12 px), the named blur/duration tokens (v4 accepts numeric `duration-120`
  natively), and the `--ease-out`/`--ease-in-out` overrides that silently
  changed what the stock utilities meant. Custom `@theme` tokens remain only
  where Balance is genuinely opinionated: colors, font families,
  `--shadow-overlay`, and the two supported global motion knobs
  (`--default-transition-duration`/`-timing-function`) as literals.

The shared picker also moved and renamed: `components/Combobox.tsx` →
`components/ui/ComboBox.tsx` (React Aria casing), with the create sentinel
gated on `onCreate` and its label defaulting inside the component.
