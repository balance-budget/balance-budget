# 0022 — Frontend i18n: per-user language and region formatting, Lingui for strings

## Status

Accepted (2026-06-09)

> Note on the number: `0022` previously held a component-library ADR (shadcn/ui
> on Base UI, committed `56b956c` and reverted the same day, `62eaad0`) — the
> story is told in [ADR-0024](0024-react-aria-components-for-spa-ui.md), which
> still references "ADR-0022" in that historical sense. That ADR file was never
> kept; the number has been reclaimed for this decision. The superseding
> component-library decision lives in ADR-0024.

## Context

The SPA ships English text as hardcoded JSX literals across ~24 screens and
~48 components (≈300–500 user-facing strings), with no extraction or
translation layer. Separately, [ADR-0024](0024-react-aria-components-for-spa-ui.md)
deliberately moved date/number rendering to *browser-locale* defaults ("a Dutch
browser shows `dd-mm-jjjj` segments and `1.234,56`; the app stops pretending
everyone reads ISO"). In practice that left two rough edges:

- A US/`en-US` browser renders **American `MM/DD/YYYY`** dates, which is the
  primary complaint driving this work.
- `lib/money.ts` never actually became locale-aware — it hardcodes
  `toLocaleString('en-US')` for grouping and a literal `.` decimal mark, with a
  couple of stray `toLocaleDateString()` calls elsewhere.

Two goals: (1) eliminate untranslated magic strings and keep them out for good;
(2) decouple *language* from *formatting* so the UI can stay **English** while
dates render **ISO** (or Dutch) rather than American — as a **user choice**, not
a function of the browser.

The constraint that shapes the formatting design: React Aria's editable widgets
(`DateField`/`NumberField`/`Calendar`) have **no format-pattern API**. The whole
stack — RAC → `@internationalized/date`'s `DateFormatter` → `Intl.DateTimeFormat`
— is locale-plus-options, never a pattern string (`"dd-MM-yyyy"` cannot be
expressed). Segment order and separators are derived from the single locale
passed to `I18nProvider`, which Adobe documents as the supported per-app
override of the browser default.

## Decision

**Two independent, per-user, backend-persisted settings**, composed where the
platform needs a single locale:

- `language` — drives translated UI strings *and* RAC's own built-in labels
  (RAC ships strings for 30+ locales). Only `en` is offered now; the
  infrastructure makes adding `nl` a second catalog plus a language option.
- `regionFormat` — a named choice (`iso` / `dmy` / `mdy`, default **`iso`**),
  plus a number-grouping style (default conventional `1,234.56`). It governs
  date order and separators; it never changes which language text is shown.

### Formatting

- All date/number/money display flows through one module —
  `lib/i18n/format.ts` (`formatDate`/`formatNumber`/`formatCurrency`) — built on
  `Intl` with **explicit field options**, and on RAC's `useDateFormatter` /
  `useNumberFormatter` when reactivity to the live setting is wanted. ISO dates
  are produced from the canonical ISO string / `CalendarDate.toString()`, never
  from `Date.prototype.toISOString()` (which converts to UTC and can shift the
  day).
- RAC editable widgets receive a **best-fit BCP-47 backing tag** through
  `I18nProvider`, derived from `regionFormat`: `iso → en-CA`, `dmy → en-GB`,
  `mdy → en-US`. These tags are an implementation detail; users only ever see
  the named choice. Consequence: a typed-input combination outside what one tag
  expresses (e.g. ISO date order *and* Dutch `1.234,56` separators at once) is
  not faithfully reproducible *while editing*, though display formatters can
  still render any combination.
- `lib/money.ts` keeps its hand-rolled split (ADR-0002) but its separators
  become a **deliberate** choice fed by `regionFormat`, defaulting to
  conventional dot/comma, replacing the hardcoded `en-US`.

### Strings — Lingui

- `@lingui/*` with macros (`<Trans>`, `t`, `plural`) and the Vite plugin.
  **Generated-from-source message IDs**: the English source text *is* the `en`
  catalog; no hand-authored key namespace.
- A `no-literal-string` ESLint rule guards against new un-wrapped JSX text
  (landed as a warning while the allowlist is tuned, then promoted to error in
  CI — matching `TreatWarningsAsErrors` discipline).
- CI runs `lingui extract` then `git diff --exit-code` on the `.po` catalogs —
  an un-extracted string fails CI, mirroring the existing `*.gen.ts` drift gate.
  `lingui compile` runs before `npm run build`.
- **Hard boundary:** Lingui handles *language only*. Lingui's own
  `i18n.date`/`i18n.number` helpers are **not** used — formatting stays in the
  format module. One source of truth per concern.

### Timezone — no setting

The domain is overwhelmingly **calendar dates** (`JournalEntry.Date`,
`BookingDate`, `ValueDate`, the inclusive Reporting period), which have no
timezone. The only true instants are audit-flavoured (`CreatedAt`/`UpdatedAt`/
`DismissedAt`, the displayed `token.createdAt`).

- **Calendar dates are never timezone-converted** — formatted as plain
  ISO / `CalendarDate`.
- **Instants render in the browser's local timezone** via `Intl`, and are *not*
  tied to `regionFormat` (timezone ≠ format).
- No user-facing timezone control. The server's "today" boundary
  (ADR-0021) follows the container's `TZ` env var, which is the single place a
  zone enters the domain.

### Persistence

`language` / `dateFormat` / `numberFormat` become three nullable string columns
on `BalanceUser` (ASP.NET Identity entity, ADR-0015/0016), surfaced on
`GET /api/auth/me` and written via a new `PATCH /api/auth/me/preferences`. The
backend stores three opaque strings it never interprets; its own output stays
English/invariant.

## Considered alternatives

1. **react-i18next** — most popular, but message keys are plain strings, giving
   weaker compile-time protection against missing/typo'd keys — the opposite of
   the "no magic strings" goal. **Paraglide** — fully type-safe and tiny, but a
   smaller/younger ecosystem. Lingui won on extraction + lint enforcement with a
   mature ecosystem.
2. **A date format-pattern setting** (`"dd-MM-yyyy"`) — impossible: RAC/Intl
   have no pattern concept. The backing-tag mechanism is the only lever for the
   editable widgets.
3. **One locale coupling language to formatting** — rejected: to get Dutch
   separators you must accept Dutch month names and RAC labels; we want English
   text with ISO/European formatting.
4. **`localStorage` persistence** — rejected: per-*browser*, not per-*login*;
   doesn't follow a user across devices and bleeds across logins on a shared
   machine.
5. **A timezone setting** — rejected: the domain is calendar dates; introducing
   conversion invites the classic off-by-one bug for zero real benefit.

## Consequences

- **Supersedes the formatting stance of ADR-0024**: rendering is no longer
  "whatever the browser says" — it's an explicit per-user setting defaulting to
  ISO. ADR-0024's component-layer decision otherwise stands.
- New dependencies: `@lingui/core`, `@lingui/react`, `@lingui/macro`,
  `@lingui/vite-plugin`, `@lingui/cli` (dev), `eslint-plugin-lingui` (dev).
- CI gains a catalog extract-drift gate and a `compile` step; lint gains
  `no-literal-string`.
- Backend gains three nullable columns and a migration per provider
  (Sqlite + Postgres); output language is unchanged.
- The `en-CA`/`en-GB`/`en-US` backing tags are never user-visible, and typed
  editable-widget formatting is limited to what a single tag can express.
- Adding a language later (`nl`) is: a second `.po`, a language option, and
  translating RAC's built-ins comes free.
