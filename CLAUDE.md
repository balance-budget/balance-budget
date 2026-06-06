# CLAUDE.md

Guidance for Claude Code (and any other AI agent or human) working in this repository.

If you are adding documentation, prefer extending the files under `docs/` and updating the index below. Use [Mermaid](https://mermaid.js.org/) for any diagrams or charts — GitHub renders them natively. Do not draw diagrams as ASCII art.

## Quick links

- [Architecture](docs/architecture.md) — layers, project graph, DI composition, request/host startup
- [Project layout](docs/project-layout.md) — where things live and where new code should go
- [Conventions](docs/conventions.md) — coding patterns enforced by the codebase
- [Getting started](docs/getting-started.md) — local dev, build, test, run, configuration

## Commands

```bash
# Restore
# CLI tools (CSharpier)
dotnet tool restore
# NuGet packages      
dotnet restore
# Frontend npm packages (npm workspaces — single root node_modules covers Balance.Web.Client)
npm install

# Build (without restore) — also builds the SPA via the esproj reference
dotnet build --no-restore -v:minimal

# Generate EF core migrations (without build)
# Add migration for PostgreSQL
dotnet ef migrations add <name> --no-build --project src/Balance.Data.PostgreSql/Balance.Data.PostgreSql.csproj --startup-project src/Balance.Web/Balance.Web.csproj -- --Database:Provider=Postgres
# Add migration for SQLite
dotnet ef migrations add <name> --no-build --project src/Balance.Data.Sqlite/Balance.Data.Sqlite.csproj --startup-project src/Balance.Web/Balance.Web.csproj -- --Database:Provider=Sqlite

# Format
# CI check
dotnet csharpier check . --ignore-path .csharpierignore
# Auto-format
dotnet csharpier format . --ignore-path .csharpierignore

# Test (without build)
# TUnit suite, runs with coverage in CI
dotnet test --no-build -v:minimal
# Single TUnit test
dotnet test --no-build -v:minimal --treenode-filter "/Balance.Tests/Balance.Tests.Domain/MoneyTests/Equality_uses_amount_and_currency"

# Run — two terminals during development
# .NET host (serves /api/*, and the last built SPA at /)
dotnet run --project src/Balance.Web/Balance.Web.csproj
# Vite dev server (HMR; proxies /api → http://localhost:5248) — browse this URL
npm run dev

# Publish — bundles the SPA's dist into the ASP.NET publish output
dotnet publish src/Balance.Web/Balance.Web.csproj -c Release
```

The .NET host listens on `http://*:5248` (and `https://*:7189` via the `https` launch profile). The Vite dev server listens on `http://localhost:5173` by default and proxies `/api/*` to the .NET host. All backend routes are under `/api`: Scalar UI at `/api/docs/`, OpenAPI document at `/api/openapi/{document}.json`, liveness probe at `/api/healthz/live`, readiness probe at `/api/healthz/ready`. The React SPA owns everything else (served via `MapStaticAssets()` with `MapFallbackToFile("index.html")`).

## Architecture at a glance

Three-layer onion ASP.NET Core app targeting `net10.0`, with a React + Vite SPA shipped inside the same publish output. Solution is `Balance.slnx` (the new XML solution format).

```mermaid
graph LR
    Web[Balance.Web]
    Client[Balance.Web.Client]
    Services[Balance.Services]
    Data[Balance.Data]
    Configuration[Balance.Configuration]
    Sqlite[Balance.Data.Sqlite]
    Postgres[Balance.Data.PostgreSql]
    IntegrationIng[Balance.Integration.Ing]

    Web -->|esproj, ReferenceOutputAssembly=false| Client
    Web --> Services
    Services --> Data
    Data --> Configuration

    Web --> Sqlite
    Web --> Postgres
    Sqlite -->|migrations| Data
    Postgres -->|migrations| Data

    Web --> IntegrationIng
    IntegrationIng -->|implements IBankTransactionExtractor| Services
```

**Layers**
- `Balance.Web` — Minimal APIs (all under `/api`), middleware pipeline, Scalar/OpenAPI, and the SPA host (`MapStaticAssets()` + `MapFallbackToFile("index.html")`). Uses `WebApplication.CreateSlimBuilder`.
- `Balance.Web.Client` — React 19 + TypeScript + Vite SPA. Built via an `.esproj` (`Microsoft.VisualStudio.JavaScript.Sdk`) that wraps `npm run build`; the resulting `dist/` is packed into the ASP.NET publish output via the project reference on `Balance.Web` (`ReferenceOutputAssembly="false"`).
- `Balance.Services` — Business logic, Quartz jobs, `IApplicationVersionService`, and the bank-import contract `IBankTransactionExtractor`. Composes `Configuration` + `Data` + `Jobs`.
- `Balance.Integration.Ing` — ING bank-statement importers. References `Balance.Services` and implements `IBankTransactionExtractor`; composed at the host *beside* Services (a third `AddBalanceIntegrationIng()` call), not nested under it, to avoid a Services↔Integration reference cycle. See `docs/adr/0018-integration-layer-composed-at-host.md`. New banks are new `Balance.Integration.<Bank>` projects.
- `Balance.Data` — EF Core `BalanceDbContext` (also implements `IDataProtectionKeyContext`), abstract `BaseEntity` (`Id`/`CreatedAt`/`UpdatedAt`), migration host extension, UTC value converters.
- `Balance.Data.Sqlite` / `Balance.Data.PostgreSql` — Provider-specific migrations assemblies (referenced by `Balance.Web`, not by `Balance.Data`).
- `Balance.Configuration` — Options pattern. `IOptionsSection` static-abstract contract, `DatabaseOptions` selecting `Sqlite` or `Postgres`, host environment helpers.
- `Balance.Tests` — TUnit suite. `InternalsVisibleTo` is set from `Balance.Web` and `Balance.Services`.

## Key conventions

These are conventions to follow when adding new code. See [docs/conventions.md](docs/conventions.md) for examples.

- **Use idiomatic C# with the latest language features.**
- **DI composition.** Each layer exposes a `public static class ServiceCollectionExtensions` with a single `AddBalance<Layer>(...)` extension. A layer composes its dependencies by calling the lower layer's `AddBalance*` inside its own. The web entry point calls `AddBalanceServices` + `AddBalanceIntegrationIng` + `AddBalanceWeb` — the integration layer is the documented exception that composes beside Services rather than nesting (ADR-0018); all other layers nest.
- **Options.** Strongly-typed options classes live under `Balance.Configuration/Options`, implement `IOptionsSection` (static-abstract `Section` name), and are wired through `AddSettings<T>` in `Balance.Configuration.ServiceCollectionExtensions`.
- **Database provider.** Selected at runtime via `Database:Provider` (`Sqlite` or `Postgres`). The provider switch lives in `Balance.Data/Helpers/DbContextOptionsBuilderExtensions.UseProvider`. Migrations live in the provider-specific assemblies; `BalanceDbContext` is provider-agnostic.
- **Entities.** Derive from `Balance.Data.Entities.BaseEntity` (`Id` `int`, `CreatedAt` `init`, `UpdatedAt`). All `DateTime` columns must round-trip as UTC via `DateConverters.UtcConverter`.
- **Constructors.** Do not use C# 12 primary constructors. Stick to the explicit pattern: `private readonly` fields plus a named constructor that assigns them.
- **Logging.** Use the source-generated `LoggerMessage` pattern. Each project has a `Logging/LoggerExtensions.cs` partial class; add `[LoggerMessage]` methods there rather than calling `ILogger.LogXxx` directly.
- **API surface.** All backend routes are mounted inside `var api = app.MapGroup("/api");` in `Program.cs`. Register feature endpoints through a `MapXxxEndpoints` extension and call it on `api`, not on `app` — the SPA fallback (`MapFallbackToFile("index.html")`) owns every non-`/api` URL.
- **Frontend.** The React SPA lives at `src/Balance.Web.Client`. Pages and components go under `src/`, public assets (favicon, etc.) under `public/`. The Vite dev server (`npm run dev`) proxies `/api` to the .NET host (`http://localhost:5248`) per `vite.config.ts`.
- **Background jobs.** Use the Quartz helpers in `Balance.Services/Jobs` (`ScheduleJobAt<TJob>(cron)`). The scheduler name is `"Balance Scheduler"`. Wire jobs inside `AddBalanceJobs`.
- **Visibility.** Default to `internal`; expose `public` only where another project legitimately needs the type. `Balance.Web` and `Balance.Services` use `InternalsVisibleTo` to share internals with `Balance.Tests`.
- **Formatting.** CSharpier is the source of truth — CI fails on any deviation. Always run `dotnet csharpier format .` before committing.
- **Commits.** Use [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary` in the imperative mood. Types: `feat`, `fix`, `docs`, `chore`, `refactor`, `style`, `test`, `ci`, `build`, `perf`, `revert`. Scope (optional, lowercase) names the area (`reports`, `ing`, `client`/`spa`, `data`, `web`, `accounts`, `bank-accounts`, `bank-transactions`, `dashboard`, `sidebar`, `auth`, `settings`, …). Reference issues/PRs in a footer (`Refs: #NN`, `Closes #NN`), not the subject. Breaking changes use `type(scope)!:`. See [docs/conventions.md](docs/conventions.md#commit-messages).
- **Build hygiene.** `Directory.Build.props` enforces `TreatWarningsAsErrors=true`, nullable enabled, `AnalysisMode=All`, `LangVersion=latest`, and `UseArtifactsOutput=true`. Package versions are centralised in `Directory.Packages.props` — never pin a version inside a `.csproj`.

## Runtime composition

The web host startup follows this order:

1. `MapConfigurationSources` — patches JSON config providers to read from `AppContext.BaseDirectory` so the solution-root `appsettings.json` works when running from source.
2. `AddBalanceServices` → `AddBalanceConfiguration` → `AddBalanceData` (registers `BalanceDbContext`, factory, and Data Protection persistence) → `AddBalanceJobs` (Quartz hosted service) → `IApplicationVersionService`.
3. `AddBalanceWeb` — OpenAPI, lowercase routing, forwarded headers (trust any proxy IP, the app is assumed to sit behind a reverse proxy), cookie auth, antiforgery, permissive CORS, health checks.
4. `MigrateDatabaseAsync(cancellationToken)` runs `dbContext.Database.MigrateAsync` on startup, logged through `Balance.Data/Logging/LoggerExtensions`.
5. SPA hosting — `MapStaticAssets()` then `MapFallbackToFile("index.html")`. The SPA's `dist/` (from `Balance.Web.Client`) is bundled into the publish output via the esproj project reference.
6. API endpoints — every backend route is registered on `var api = app.MapGroup("/api")`: `/api/healthz/{live,ready}`, `/api/openapi/...`, `/api/docs/` (Scalar), and the feature endpoint groups.
7. Middleware order (in `Program.cs`): `ExceptionHandler → StatusCodePages → ForwardedHeaders → DefaultFiles → Routing → CORS → Authentication → Authorization → Antiforgery`.

## CI

`.github/workflows/build-and-test.yml`: `dotnet tool restore` → `dotnet restore` → CSharpier check → build → frontend ESLint (`npm run lint` from the repo root, which forwards to the `Balance.Web.Client` workspace and reuses the SPA build the esproj already ran) → CodeQL (public repos) → `dotnet test` with cobertura coverage. Test results and coverage are posted as sticky PR comments. A separate scheduled `codeql.yml` re-runs CodeQL weekly.

## Notes for AI agents

- **Always run CSharpier** (`dotnet csharpier format .`) after writing C# — CI fails otherwise and `TreatWarningsAsErrors=true` will catch a lot too.
- **Match the existing DI pattern** when adding a new layer or feature module: a single `AddBalance*` extension that internally composes its dependencies.
- **Don't pin package versions in `.csproj`** — add or update the `PackageVersion` entry in `Directory.Packages.props`.
- **EF Core migrations** must be generated against the provider-specific assembly: The migrations assembly is wired up in `DbContextOptionsBuilderExtensions.UseProvider`.
-- **Don't suppress warnings.** Don't use #pragma or SuppressMessageAttribute. Only use .editorconfig and only for global rules. Always try to fix the issue in code first.  

## Agent skills

### Issue tracker

Issues live on GitHub at `balance-budget/balance-budget`, managed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
