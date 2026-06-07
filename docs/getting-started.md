# Getting started

## Prerequisites

- .NET SDK **10.0.300** (or any `10.0.x` that satisfies `rollForward=latestMinor`). The version is pinned in `global.json`.
- **Node.js 20+** for the React + Vite frontend (`Balance.Web.Client`). Vite 8 requires Node 20.19+ / 22.12+; CI uses Node 24.
- An IDE that understands the new `.slnx` solution format — Rider, Visual Studio 2022 17.11+, or VS Code with the C# Dev Kit.
- (Optional) PostgreSQL 14+ if you want to run against the Postgres provider instead of SQLite.

## First-time setup

```bash
dotnet tool restore      # installs CSharpier as a local tool
dotnet restore           # restores NuGet packages
npm install              # restores SPA dependencies (npm workspace → root node_modules)
dotnet build             # server-only; the SPA builds separately via npm (ADR-0023)
```

The repo is an [npm workspace](https://docs.npmjs.com/cli/v10/using-npm/workspaces): the SPA at `src/Balance.Web.Client` is declared as a workspace in the root `package.json`, so a single `npm install` at the repo root covers everything and produces one root `node_modules/`.

## Common commands

```bash
# Build (server-only; emits artifacts/openapi/Balance.Web.json for codegen)
dotnet build --no-restore

# Regenerate the typed API client after changing the backend API surface
# (output src/lib/api-types.gen.ts is committed; CI fails if it drifts)
npm run codegen

# Format (CI fails if these report differences)
dotnet csharpier check .
dotnet csharpier format .

# Test (TUnit via Microsoft.Testing.Platform)
dotnet test

# Run — two terminals during development
# Terminal 1: .NET host (serves /api/*, and the last npm-built SPA at /)
dotnet run --project src/Balance.Web/Balance.Web.csproj

# Terminal 2: Vite dev server with HMR — browse http://localhost:5173
npm run dev

# Publish (npm run build first — Vite outputs to src/Balance.Web/wwwroot, which the
# static-web-assets pipeline discovers and packs into the publish output, ADR-0023)
npm run build
dotnet publish src/Balance.Web/Balance.Web.csproj -c Release
```

During development, point your browser at the **Vite dev server** (`http://localhost:5173`) — it serves the SPA with HMR and proxies `/api/*` to the .NET host on `:5248` per `src/Balance.Web.Client/vite.config.ts`. The .NET host's `/` serves whatever `npm run build` last left in `src/Balance.Web/wwwroot/` — possibly nothing on a fresh clone, and stale until you rebuild. Useful only for sanity-checking the published shape.

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

`MigrateDatabaseAsync` runs on host startup, so `dotnet ef database update` is normally unnecessary. To create a new migration:

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

The React SPA owns `/` and any non-`/api` URL (via `MapFallbackToFile("index.html")`). All backend routes are mounted under `/api`:

| Route                              | Purpose                                                      |
|------------------------------------|--------------------------------------------------------------|
| `/`                                | React SPA shell (fallback to `index.html`)                   |
| `/api/docs/`                       | Scalar API reference UI                                      |
| `/api/openapi/{document}.json`     | OpenAPI document (consumed by Scalar)                        |
| `/api/healthz/live`                | Liveness probe (always 200 while the process is up)          |
| `/api/healthz/ready`               | Readiness probe (200 when the DB is reachable, else 503)     |
| `/api/{feature}/…`                 | Feature endpoint groups (currencies, accounts, journal-entries, …) |

Launch profiles (`src/Balance.Web/Properties/launchSettings.json`):
- `http` → `http://*:5248`
- `https` → `https://*:7189;http://*:5248`

Vite dev server (default, no profile — set via `vite.config.ts`):
- `http://localhost:5173` (HMR + `/api` → `http://localhost:5248` proxy)

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
