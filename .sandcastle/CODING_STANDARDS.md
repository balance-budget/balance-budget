# Coding Standards

The reviewer agent loads this file during code review via `@.sandcastle/CODING_STANDARDS.md`
so the standards are enforced during review without costing tokens during implementation.

The authoritative sources are `CLAUDE.md` and `docs/conventions.md` at the repo root —
read them. The highlights the reviewer should hold the change to:

## Backend (C#)

- **Formatting is non-negotiable.** CSharpier is the source of truth; CI fails on any
  deviation. Run `dotnet csharpier check . --ignore-path .csharpierignore`.
- **Warnings are errors.** `Directory.Build.props` sets `TreatWarningsAsErrors=true`,
  nullable enabled, and `AnalysisMode=All`. Do **not** suppress warnings with `#pragma`
  or `SuppressMessageAttribute` — fix them in code; global rules go in `.editorconfig`.
- **No primary constructors.** Use explicit `private readonly` fields plus a named
  constructor that assigns them.
- **DI composition.** Each layer exposes one `AddBalance<Layer>` extension that composes
  the layer below it (the ING integration is the documented exception, composed beside
  Services — ADR-0018).
- **Logging.** Use the source-generated `[LoggerMessage]` pattern in each project's
  `Logging/LoggerExtensions.cs`, not `ILogger.LogXxx` directly.
- **API surface.** Backend routes mount on the `/api` `MapGroup`, never on `app`.
- **Entities** derive from `BaseEntity`; all `DateTime` columns round-trip as UTC via
  `DateConverters.UtcConverter`.
- **Package versions** are centralized in `Directory.Packages.props` — never pin a
  version in a `.csproj`.

## Frontend (SPA)

- React 19 + TypeScript + Vite under `src/Balance.Web.Client`. No `any` or unchecked
  casts.
- **All user-facing copy goes through Lingui** (lint-enforced — ADR-0022). When you add
  or change a string, run `npm run extract` and supply the translation in every locale
  (`en`, `nl-NL`, `zh-TW`) — the `src/locales/` catalogs are drift-gated and must not
  contain empty `msgstr`s.
- Keep `.gen.ts` artifacts (the typed API client, `routeTree.gen.ts`) in sync — CI gates
  on drift.

## General

- **US English everywhere** — identifiers, comments, UI copy, docs (`categorize`,
  `color`, `behavior`). Spell out "journal entry"; avoid gratuitous em-dashes.
- Prefer clarity over brevity; avoid nested ternaries (use `switch` or `if`/`else`).
- New behavior must be covered by tests (TUnit for the backend, vitest for the SPA).
- Commits follow Conventional Commits (`type(scope): summary`).
