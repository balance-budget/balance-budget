# Architecture

Balance Budget is a Clean Architecture ASP.NET Core 10 application. The codebase is currently at the "basic layout" stage — the personal finance domain has not yet been designed. This document describes the skeleton that new domain code will plug into.

## Solution

The solution file is `Balance.slnx` (the modern XML solution format — not `.sln`).

```
Balance.slnx
├── /solution/          Build & repo-level files (.editorconfig, props, README, …)
├── /src/
│   ├── Balance.Configuration
│   ├── Balance.Data
│   ├── Balance.Data.PostgreSql
│   ├── Balance.Data.Sqlite
│   ├── Balance.Services
│   ├── Balance.Web
│   └── Balance.Console
└── /tests/
    └── Balance.Tests
```

## Project graph

```mermaid
graph TD
    Web["Balance.Web<br/>(Microsoft.NET.Sdk.Web, exe)"]
    Console["Balance.Console<br/>(Microsoft.NET.Sdk, exe)"]
    Sqlite[Balance.Data.Sqlite]
    Postgres[Balance.Data.PostgreSql]
    Services[Balance.Services]
    Data[Balance.Data]
    Configuration[Balance.Configuration]

    Web --> Sqlite
    Web --> Services
    Web --> Postgres
    Console --> Sqlite
    Console --> Services
    Console --> Postgres

    Sqlite --> Data
    Services --> Data
    Postgres --> Data

    Data --> Configuration
```

Notes:
- `Balance.Data` does **not** reference the provider-specific projects. It targets EF Core's relational abstractions and lets the host process load the right migrations assembly at runtime.
- `Balance.Web` and `Balance.Console` both reference the provider-specific projects directly so their assemblies are loaded for migrations.
- `Balance.Tests` references `Balance.Web` and `Balance.Services` (transitively pulling in the rest). Both expose internals via `InternalsVisibleTo("Balance.Tests")`.

## Layers

### Balance.Configuration

- `IOptionsSection` — a static-abstract contract requiring `static string Section`. All options classes implement it so the binding helper can locate their config section by type.
- `Options/DatabaseOptions` — `Provider` (`Sqlite` | `Postgres`) and `ConnectionString`.
- `Helpers/HostEnvironmentExtensions` — `IsContainer()` and `IsContainerFastMode()` based on the `DOTNET_RUNNING_IN_CONTAINER` / `…_FAST_MODE` env vars.
- `Helpers/ConfigurationExtensions` — `GetSection<T>()` and `GetSectionOrDefault<T>()` typed lookups.
- `ServiceCollectionExtensions.AddBalanceConfiguration` registers all known options sections (currently just `DatabaseOptions`).

### Balance.Data

- `BalanceDbContext` (in `SpottarrDbContext.cs` — see [Known oddities](#known-oddities)) — implements `IDataProtectionKeyContext` so ASP.NET Data Protection keys persist to the same database. Exposes `Provider` so consumers can branch on dialect when necessary. Enables `EnableDetailedErrors` / `EnableSensitiveDataLogging` in development only.
- `Entities/BaseEntity` — `Id` (`int`), `CreatedAt` (`init`), `UpdatedAt`. All domain entities should derive from this.
- `Helpers/DbContextOptionsBuilderExtensions.UseProvider` — the provider switch. Returns a `UseSqlite(...).UseBulkInsertSqlite()` or `UseNpgsql(...).UseBulkInsertPostgreSql()` builder, wiring the appropriate migrations assembly.
- `Helpers/DbPathHelper` — picks the SQLite file path (`/data/balance.db` in containers, `%LOCALAPPDATA%/balance-budget/balance.db` otherwise) and proactively probes for write access.
- `Helpers/DateConverters` — `UtcConverter` and `UtcNullableConverter` ensure `DateTime` columns round-trip with `Kind = Utc`.
- `Helpers/HostExtensions.MigrateDatabase` — applies pending migrations at host startup and logs through the source-generated logger.
- `Helpers/DatabaseFacadeExtensions` — `Vacuum()` and `Analyze()` raw-SQL helpers.
- `Logging/LoggerExtensions` — partial class for source-generated `[LoggerMessage]` methods.
- `ServiceCollectionExtensions.AddBalanceData` registers `BalanceDbContext` (and its factory) plus ASP.NET Data Protection persisted to the same context with application name `"Balance"`.

### Balance.Data.Sqlite / Balance.Data.PostgreSql

Empty class libraries that exist solely to host provider-specific EF Core migrations. They reference `Balance.Data` plus the relevant EF Core provider package. The runtime selects the matching assembly via `MigrationsAssembly("Balance.Data.Sqlite" | "Balance.Data.PostgreSql")`.

### Balance.Services

- `ApplicationVersionService` / `IApplicationVersionService` — reads `AssemblyInformationalVersionAttribute` from the entry assembly; falls back to `"0.0.0"`.
- `Jobs/JobsServiceCollectionExtensions.AddBalanceJobs` — registers Quartz with `SchedulerName = "Balance Scheduler"` and the hosted service that waits for application startup and job completion on shutdown.
- `Jobs/ServiceCollectionQuartzConfiguratorExtensions.ScheduleJob<TJob>` — schedules a job with a cron expression, optional immediate start, and `DisallowConcurrentExecution`.
- `Jobs/TriggerConfiguratorExtensions.StartNow(bool)` — conditional variant of Quartz's `StartNow()`.
- `Logging/LoggerExtensions` — partial class for source-generated `[LoggerMessage]` methods.
- `ServiceCollectionExtensions.AddBalanceServices` composes `Configuration` + `Data` + `Jobs` and registers `IApplicationVersionService`. The `startJobs` parameter (default `true`) flips whether jobs trigger immediately — the Console host passes `false`.

### Balance.Web

Built with `WebApplication.CreateSlimBuilder` for fast startup and minimal default services. Uses workstation GC (`<ServerGarbageCollection>false</ServerGarbageCollection>`) because the app is expected to run in resource-constrained containers.

- `Program.cs` — startup, in this order: configure logging, remap config sources, compose services, build, run database migrations, map endpoints (`/healthz`, static assets, HTMX, OpenAPI, Scalar), then the middleware pipeline.
- `Endpoints/HtmxEndpoints` — HTMX fragment routes under `/htmx/*`. Each is excluded from OpenAPI and returns `HtmlResult`.
- `EndpointResults/HtmlResult` — `IResult` that writes raw HTML with `text/html` content type and a correct `Content-Length`.
- `Configuration/ConfigurationManagerExtensions.MapConfigurationSources` — in development and container-fast-mode, points JSON config providers at `AppContext.BaseDirectory` so the solution-root `appsettings.json` is found when running from source.
- `Logging/LoggingBuilderExtensions.AddConsole` — single-line console logger with timestamps in containers, default console logger otherwise.
- `ServiceCollectionExtensions.AddBalanceWeb` — OpenAPI, lowercase route options, forwarded-headers (proxy IP whitelist cleared — the app trusts any proxy by assumption that it never sits on the public internet directly), cookie authentication, authorization, antiforgery, permissive default CORS policy, health checks.
- `wwwroot/` — static front-end with HTMX (`htmx.org@2.0.4`) loaded from a CDN, served via `MapStaticAssets()` (ASP.NET Core 9+ static-web-assets pipeline).

### Balance.Console

A standalone `Host.CreateApplicationBuilder` entry point that shares the same service graph. Passes `startJobs: false` to `AddBalanceServices` so Quartz triggers do not fire on startup. Currently a scaffold for one-off operations (migrations, admin commands).

### Balance.Tests

TUnit-based test suite. `Microsoft.Testing.Platform` is the configured test runner (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`). CI emits cobertura coverage and a GitHub-flavoured Markdown summary that is posted back to the PR.

## Startup composition

Both entry points follow this shape:

```
builder.Logging.AddConsole(...)
builder.Configuration.MapConfigurationSources(...)          // Web only
builder.Services.AddBalanceServices(builder.Configuration)  // Console passes startJobs:false
builder.Services.AddBalanceWeb()                            // Web only
var app = builder.Build();
await app.MigrateDatabase(lifetime.ApplicationStopping);
// map endpoints / configure middleware (Web)
await app.RunAsync(lifetime.ApplicationStopping);
```

Web middleware order is fixed in `Program.cs`:

```
ForwardedHeaders → DefaultFiles → Routing → CORS → Authentication → Authorization → Antiforgery
```

## Build and CI

- `Directory.Build.props` (applies to every project):
  - `TargetFramework=net10.0`
  - `TreatWarningsAsErrors=true`
  - `Nullable=enable`, `ImplicitUsings=enable`
  - `EnableNETAnalyzers=true`, `AnalysisMode=All`, `AnalysisLevel=latest`
  - `LangVersion=latest`
  - `UseArtifactsOutput=true` — all build output goes under `/artifacts/`, gitignored.
- `Directory.Packages.props` — centralised package versions (`ManagePackageVersionsCentrally=true`).
- `global.json` — pins SDK `10.0.300` with `rollForward=latestMinor`, registers `Microsoft.Testing.Platform` as the test runner.
- `dotnet-tools.json` — CSharpier `1.2.6` as the project-local formatter.
- `.editorconfig` — minimal; disables `CA2007` (no `ConfigureAwait` on tasks in app code).
- CI (`.github/workflows/build-and-test.yml`):
  1. `dotnet tool restore`
  2. `dotnet restore`
  3. `dotnet csharpier check .`
  4. `dotnet build --no-restore`
  5. CodeQL analyze (public repos only)
  6. `dotnet test` with cobertura coverage
  7. Sticky PR comments for test results and coverage
- A separate `codeql.yml` re-runs CodeQL on a weekly cron.

## Known oddities

- `src/Balance.Data/SpottarrDbContext.cs` contains the class `BalanceDbContext`. The filename is a leftover from a fork from the [Spottarr](https://github.com/christiaanderidder/spottarr) project; the class name is canonical. Treat the class name as authoritative.
