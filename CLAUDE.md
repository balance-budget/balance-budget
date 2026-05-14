# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore
dotnet tool restore          # Restore CLI tools (CSharpier)
dotnet restore               # Restore NuGet packages

# Build
dotnet build --no-restore

# Format
dotnet csharpier check .     # Check formatting (run in CI)
dotnet csharpier write .     # Auto-format code

# Test
dotnet test                  # Run all tests with coverage

# Run
dotnet run --project src/Balance.Web/Balance.Web.csproj
```

## Architecture

Clean Architecture ASP.NET Core 10.0 app targeting `net10.0`. Solution file is `Balance.slnx`.

**Layers:**
- `Balance.Web` — Minimal APIs, HTMX endpoints, middleware pipeline, Scalar/OpenAPI docs
- `Balance.Services` — Business logic, Quartz background jobs, `IApplicationVersionService`
- `Balance.Data` — EF Core `BalanceDbContext` (also stores Data Protection keys via `IDataProtectionKeyContext`), abstract `BaseEntity` with `Id`/`CreatedAt`/`UpdatedAt`
- `Balance.Data.Sqlite` / `Balance.Data.PostgreSql` — Provider-specific EF Core implementations
- `Balance.Configuration` — Options pattern; `DatabaseOptions` selects `Sqlite` or `Postgres` provider via `appsettings.json`
- `Balance.Console` — Standalone console entry point
- `Balance.Tests` — TUnit test suite

**Key patterns:**
- Each layer exposes a `ServiceCollectionExtensions` with an `AddBalance*()` extension method for DI registration.
- Database provider is runtime-selectable via `Database:Provider` config (`Sqlite` or `Postgres`); connection string set via `Database:ConnectionString`.
- Web uses `WebApplication.CreateSlimBuilder()` (lightweight). Middleware order: ForwardedHeaders → DefaultFiles → Routing → CORS → Authentication → Authorization → Antiforgery.
- `HtmxEndpoints` handles HTMX-style HTML responses via custom `HtmlResult`.

**Build settings** (enforced globally via `Directory.Build.props`):
- `TreatWarningsAsErrors=true`, `Nullable=enable`, `LangVersion=latest`
- Centralized package versions in `Directory.Packages.props`
- Artifacts output via `UseArtifactsOutput=true`

**CI** (`.github/workflows/build-and-test.yml`): runs CSharpier check → build → CodeQL → test with coverage on push/PR to `main`. Test and coverage results are posted as sticky PR comments.
