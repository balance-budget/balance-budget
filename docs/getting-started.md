# Getting started

## Prerequisites

- .NET SDK **10.0.300** (or any `10.0.x` that satisfies `rollForward=latestMinor`). The version is pinned in `global.json`.
- An IDE that understands the new `.slnx` solution format — Rider, Visual Studio 2022 17.11+, or VS Code with the C# Dev Kit.
- (Optional) PostgreSQL 14+ if you want to run against the Postgres provider instead of SQLite.

## First-time setup

```bash
dotnet tool restore     # installs CSharpier as a local tool
dotnet restore          # restores NuGet packages
dotnet build            # verify everything builds
```

## Common commands

```bash
# Build
dotnet build --no-restore

# Format (CI fails if these report differences)
dotnet csharpier check .
dotnet csharpier format .

# Test (TUnit via Microsoft.Testing.Platform)
dotnet test

# Run the web host (http://localhost:5248, https://localhost:7189)
dotnet run --project src/Balance.Web/Balance.Web.csproj

# Run the console host
dotnet run --project src/Balance.Console/Balance.Console.csproj
```

## Configuration

Configuration lives in the **solution-root** `appsettings.json`:

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": ""
  }
}
```

Override with environment variables using the standard ASP.NET double-underscore syntax:

```bash
Database__Provider=Postgres Database__ConnectionString="Host=localhost;Database=balance;Username=balance;Password=…" \
  dotnet run --project src/Balance.Web/Balance.Web.csproj
```

### Database providers

- **`Sqlite` (default).** File path is chosen by `DbPathHelper`:
  - In containers (`DOTNET_RUNNING_IN_CONTAINER=true`): `/data/balance.db`
  - Otherwise: `%LOCALAPPDATA%/balance-budget/balance.db` (e.g. `~/Library/Application Support/balance-budget/balance.db` on macOS)
  - The helper probes write access on startup and fails fast with a clear error if the directory is not writable.
- **`Postgres`.** Set `Database:ConnectionString` to a Npgsql-compatible connection string. The connection string is used verbatim — no helper construction.

### Migrations

`MigrateDatabase` runs on host startup, so `dotnet ef database update` is normally unnecessary. To create a new migration:

```bash
# SQLite
dotnet ef migrations add <Name> \
  --project src/Balance.Data.Sqlite \
  --startup-project src/Balance.Web \
  -- --Database:Provider Sqlite

# PostgreSQL
dotnet ef migrations add <Name> \
  --project src/Balance.Data.PostgreSql \
  --startup-project src/Balance.Web \
  -- --Database:Provider Postgres
```

The migrations assembly is wired through `UseProvider` in `Balance.Data/Helpers/DbContextOptionsBuilderExtensions.cs`.

## Endpoints exposed by the web host

| Route        | Purpose                                                       |
|--------------|---------------------------------------------------------------|
| `/`          | Static landing page (`wwwroot/index.html` via DefaultFiles)   |
| `/scalar/`   | Scalar API reference UI                                       |
| `/openapi/*` | OpenAPI document (consumed by Scalar)                         |
| `/htmx/*`    | HTMX fragment endpoints (excluded from OpenAPI)               |
| `/healthz`   | Health-check probe                                            |

Launch profiles (`src/Balance.Web/Properties/launchSettings.json`):
- `http` → `http://*:5248`
- `https` → `https://*:7189;http://*:5248`

## Adding a NuGet package

```bash
# 1. Add the version to Directory.Packages.props (alphabetical order)
#    <PackageVersion Include="Some.Package" Version="1.2.3" />

# 2. Reference it without a version in the target .csproj
#    <PackageReference Include="Some.Package" />
```

Never put a `Version=` attribute on a `PackageReference` — central package management requires the version to come from `Directory.Packages.props`.

## Troubleshooting

- **`CSharpier check` fails in CI but works locally.** You probably forgot to run `dotnet tool restore` after pulling. The lock file in `dotnet-tools.json` pins CSharpier `1.2.6`.
- **`The database path '…' is not writeable`.** `DbPathHelper` could not create or write to the SQLite data directory. In a container, mount `/data` as a writable volume; locally, check `%LOCALAPPDATA%/balance-budget` permissions.
- **`appsettings.json` not picked up when running from source.** `MapConfigurationSources` only patches paths in `Development` and `ContainerFastMode`. If you've changed the environment to something else, copy the file alongside the host binary or extend the helper.
- **EF migration commands complain about the provider.** The `-- --Database:Provider <X>` block at the end of the `dotnet ef` command is required so the runtime config selects the matching `UseProvider` branch when EF builds the design-time model.
