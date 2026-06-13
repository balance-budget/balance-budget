# 0029 — Date and number formatting: locale-driven, with an ISO override

## Status

Accepted (2026-06-13)

Supersedes the **date- and number-formatting** stance of
[ADR-0022](0022-frontend-i18n-language-and-region-formatting.md). ADR-0022's
Lingui (language-only) and per-user persistence decisions stand unchanged.

## Context

ADR-0022 introduced a 3-way `dateFormat` preference (`iso` / `dmy` / `mdy`)
*decoupled from* `language`, plus a hard boundary: "month names stay English."
The goal was English UI text with European date order.

In practice this fought `Intl`. `Intl.DateTimeFormat` takes **one locale** that
governs field order, month-name language, *and* separators together — there is
no pattern-string lever (the C# `ToString("dd MMM yyyy", culture)` model does
not exist in ECMA-402). Honoring "this language's month name in that language's
order" required hand-rolled `formatToParts` reassembly across 3 prefs × 3
languages — a mini pattern engine, for little real gain. The only intra-language
order ambiguity that actually matters is American vs British English; `nl-NL` is
always day-first and `zh-TW` always `年月日`.

## Decision

**Stop fighting `Intl`. The user's `language` locale decides date order, month
names, and number separators. One escape hatch each: a forced ISO override.**

`dateFormat` and `numberFormat` become two symmetric per-user toggles, both
`locale | iso`, both defaulting to **`iso`** (unambiguous, and the escape hatch
for anyone unhappy with their locale's native form):

- **`dateFormat: locale | iso`.** `dmy`/`mdy` are retired; persisted `dmy`/`mdy`
  values map to **`locale`** (not the `iso` default, so those users don't
  silently flip).
- **`numberFormat: locale | iso`.** The 3-way `comma-dot` / `dot-comma` /
  `space-comma` is retired and maps to **`locale`** (each was a "follow my
  region" choice). `locale` defers grouping + decimal to the `language`; `iso`
  is ISO 80000 — narrow no-break space (U+202F) groups, dot decimal:
  `1 234 567.89`.
- **Callers pass intent, not `Intl` option bags**: a granularity
  (`year` / `year-month` / `year-month-day`) plus a `style` hint
  (`short` / `long`). Locale-mode hands this to `Intl`; ISO-mode intercepts it.
- **ISO-mode** produces bare numeric from the canonical `CalendarDate`:
  `2026`, `2026-06`, `2026-06-13`. It **ignores `style` and `weekday`**, and
  renders instants as `2026-06-13 14:30` (24h). Locale-mode instants use the
  locale's own clock.
- **`en-GB` is added as a `language` option** ("English (UK)"), reusing the `en`
  Lingui catalog. This is how English-speaking day-first users get
  `13 Jun 2026` without ISO — *without* reopening a separate
  formatting-locale ↔ language split.
- **Editable RAC widgets just follow the culture.** They take a single backing
  tag (`en-CA` for ISO dates, the `language` tag otherwise); date-segment order
  and RAC's own labels follow it, and number-field separators fall out of that
  one locale. A locale-mode date is therefore *displayed* `Jun 13, 2026` but
  *edited* `06/13/2026` — accepted; RAC has no named-month input.
- **All date/number display flows through `i18n/format.ts`** — the one module
  allowed to construct `Intl` formatters or call `toLocale*`. A
  `no-restricted-syntax` lint rule bans `new Intl.DateTimeFormat` /
  `new Intl.NumberFormat` construction and `toLocale*String` everywhere else —
  CI-enforced, like the `*.gen.ts` drift gate. This is what keeps "every date
  and number adheres" true over time.

## Considered alternatives

1. **Keep ADR-0022's 3-way `dateFormat` + `formatToParts` reassembly** —
   rejected: reimplements `Intl` pattern logic, brittle across locales, for a
   distinction (`dmy`/`mdy` independent of language) only English needs.
2. **A separate formatting-locale pref distinct from UI language** — rejected:
   resurrects the language↔format matrix ADR-0022's complexity came from. `en-GB`
   as a language is the cheaper, consistent path.
3. **No ISO override, pure locale** — rejected: ISO is the unambiguous default
   and the escape hatch for anyone unhappy with their locale's native order.

## Consequences

- Reverses ADR-0022's "month names stay English": month names are now
  **localized** to `language` (`nl-NL` → `jun.`, `zh-TW` → `6月`).
- `region.ts` shrinks: both prefs are two-valued; the `en-CA`/`en-GB`/`en-US`
  date-locale map, the number-locale map, and `backingTag`'s combination table
  are retired in favor of `language`-as-locale + an ISO branch.
- `money.ts` (ADR-0002 hand-rolled split) reads `groupInteger` /
  `activeDecimalSeparator` from `format.ts`; in `iso` mode it groups with U+202F
  and a dot decimal.
- `en-GB` joins `en` / `nl-NL` / `zh-TW` as a language; it reuses the `en`
  catalog (no `en-GB` PO file, not in `lingui.config.ts`), so British readers
  see US spelling (copy is fixed US English per CLAUDE.md).
- One-time audit migrates stragglers (`Outlook.tsx` `toLocaleDateString`,
  `money.ts` `toLocaleString`, Preferences sample formatters) through
  `format.ts`; tests cover `{en, en-GB, nl-NL, zh-TW} × {locale, iso}` ×
  granularities × instants.

