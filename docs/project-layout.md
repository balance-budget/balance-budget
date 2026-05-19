# Project layout

Where things live, and where new code should go.

## Top-level

```
balance-budget/
├── Balance.slnx                 Solution (XML format)
├── Directory.Build.props        Global MSBuild props (TFM, warnings-as-errors, nullable, …)
├── Directory.Packages.props     Centralised NuGet versions
├── appsettings.json             Solution-shared config; copied into each host's output
├── dotnet-tools.json            Local tools (csharpier)
├── global.json                  SDK pin + test runner
├── .editorconfig
├── README.md
├── CLAUDE.md                    AI-agent + human entry point (see also docs/)
├── docs/                        Long-form documentation
├── img/                         Logo assets
├── src/
│   ├── Balance.Configuration/
│   ├── Balance.Data/
│   ├── Balance.Data.PostgreSql/
│   ├── Balance.Data.Sqlite/
│   ├── Balance.Services/
│   ├── Balance.Web/                 ASP.NET host (API + SPA shell)
│   └── Balance.Web.Client/          React + Vite SPA (.esproj)
├── tests/
│   └── Balance.Tests/
├── .github/                     Issue templates, workflows, dependabot, funding
└── artifacts/                   Build output (gitignored, UseArtifactsOutput=true)
```

## Where to put new code

| Adding…                                       | Where it goes                                                       |
|-----------------------------------------------|---------------------------------------------------------------------|
| Domain entity (`Account`, `Transaction`, …)   | `src/Balance.Data/Entities/`, derive from `BaseEntity`              |
| Entity configuration (`IEntityTypeConfiguration<T>`) | `src/Balance.Data/Configurations/` (new folder, see conventions) |
| `DbSet<T>` on the context                     | `src/Balance.Data/SpottarrDbContext.cs` (class `BalanceDbContext`)  |
| Domain service / use case                     | `src/Balance.Services/<Feature>/`                                   |
| Service contract / DTO consumed by Web        | `src/Balance.Services/Contracts/`                                   |
| Background job (Quartz `IJob`)                | `src/Balance.Services/Jobs/<JobName>.cs`, scheduled in `AddBalanceJobs` |
| Options class                                 | `src/Balance.Configuration/Options/`, implement `IOptionsSection`   |
| Minimal API endpoint (JSON)                   | `src/Balance.Web/Endpoints/` — new `Map<Feature>Endpoints` group, called on the `/api` route group in `Program.cs` |
| Frontend page / component                     | `src/Balance.Web.Client/src/`                                       |
| Public frontend asset (favicon, etc.)         | `src/Balance.Web.Client/public/`                                    |
| Provider-specific migration                   | `src/Balance.Data.Sqlite/Migrations/` or `…PostgreSql/Migrations/`   |
| Source-generated log message                  | `<Project>/Logging/LoggerExtensions.cs` partial class                |
| Unit / integration test                       | `tests/Balance.Tests/`                                              |

## Folder conventions inside a project

Each project follows the same shape where applicable:

```
<Project>/
├── <Project>.csproj
├── ServiceCollectionExtensions.cs   AddBalance<Project>(...) — the layer's DI entry point
├── Contracts/                       Public interfaces consumed by other layers
├── Options/                         IOptionsSection-implementing config classes  (Configuration only)
├── Entities/                        EF Core entities                              (Data only)
├── Configurations/                  IEntityTypeConfiguration<T>                   (Data — to be added)
├── Helpers/                         Extension methods, internal utilities
├── Logging/
│   └── LoggerExtensions.cs          Source-generated [LoggerMessage] methods
├── Endpoints/                       Minimal-API endpoint groups                   (Web only)
├── Jobs/                            Quartz IJob impls and helpers                 (Services only)
└── Properties/                      launchSettings.json                           (Web only)
```

`Configurations/` is the recommended home for `IEntityTypeConfiguration<T>` classes as the domain grows — it doesn't exist yet because no entities have been defined.

## Files outside the solution folders

- `appsettings.json` lives at the **solution root**, not inside `Balance.Web`. `MapConfigurationSources` in `Balance.Web/Configuration/ConfigurationManagerExtensions.cs` patches the JSON config providers' file provider to read from `AppContext.BaseDirectory` so dev runs and container-fast-mode pick it up correctly.
- `Directory.Build.props` and `Directory.Packages.props` are loaded by MSBuild from any ancestor directory. Don't duplicate them.
- `.github/` holds workflows (`build-and-test.yml`, `codeql.yml`), issue templates, dependabot config, and funding info.
