# 0023 — Replace the esproj with wwwroot static-web-assets discovery

## Status

Accepted (2026-06-07)

## Context

The SPA was wired into the .NET build through `Balance.Web.Client.esproj`
(`Microsoft.VisualStudio.JavaScript.Sdk`), referenced from `Balance.Web` with
`ReferenceOutputAssembly="false"`. The JS SDK ran `npm run build` and flowed
`dist/` into the static-web-assets manifest that `MapStaticAssets()` serves.

This caused recurring friction:

- The SDK is Visual Studio–centric and thinly maintained; its NuGet version is
  pinned inside the esproj itself, outside `Directory.Packages.props`.
- npm workspaces hoist the lockfile to the repo root; an empty
  `package-lock.json` had to be committed in the client purely to appease the
  SDK's restore detection.
- Because a `ProjectReference` builds *before* the referencing project
  compiles, the SPA always built before the server — the wrong order for
  OpenAPI-driven type generation (`dotnet build` emits the OpenAPI document
  that `npm run codegen` consumes).
- Every `dotnet build` ran the full Vite + `tsc -b` build, slowing backend
  iteration even though development uses the Vite dev server.
- The SDK ignored `dotnet publish --no-build` and re-ran the Vite build
  during the Docker publish stage anyway.
- CI could not gate on frontend lint/format/test/typegen as first-class steps
  with a sane ordering (this is why the ESLint CI gate was deferred).

### Considered alternatives

1. **Re-create the project-reference integration with custom MSBuild**
   (`DefineStaticWebAssets` extensibility). Preserves behaviour exactly but
   swaps one under-documented SDK corner for another.
2. **Plain copy + `UseStaticFiles`** (`ResolvedFileToPublish` mapping `dist/**`
   into `wwwroot/`). Simple, but abandons `MapStaticAssets` and its
   publish-time precompression, fingerprint-aware ETags, and caching headers.
3. **Build the SPA straight into `Balance.Web/wwwroot/`** and let the standard
   static-web-assets *discovery* pipeline (the original `wwwroot` source type)
   pick it up. `Program.cs` keeps `MapStaticAssets()` + `MapFallbackToFile`
   untouched.

### Decisive experiment

A spike tested whether `dotnet publish --no-build` re-runs asset discovery for
files that appear in `wwwroot/` *after* `dotnet build`. It does not: the file
was physically copied into the publish output by the Web SDK's default globs,
but received **zero endpoints** in `*.staticwebassets.endpoints.json` (the
manifest is serialized into `obj/` at build time and replayed). A request for
such a file would fall through to the SPA fallback and silently receive
`index.html`. Therefore the publish step must run the (incremental) build so
discovery happens in the same invocation as manifest generation.

## Decision

Adopt option 3. Concretely:

- Delete `Balance.Web.Client.esproj`, its `Balance.slnx` entry, and the
  `ProjectReference` in `Balance.Web.csproj`. The client is a plain npm
  workspace package; Rider users attach the folder to the Solution Explorer
  ("Attach Existing Folder", per-machine).
- Vite builds with `outDir: ../Balance.Web/wwwroot` and `emptyOutDir: true`.
  `wwwroot/` is gitignored build output; a `<MakeDir>` target in
  `Balance.Web.csproj` guarantees the web root exists for source runs.
- The SPA builds on **publish only**, and the publish is explicit:
  `dotnet build` never invokes npm. CI and the Dockerfile run
  `npm ci` / `npm run build` as first-class steps between `dotnet build` and
  `dotnet publish --no-restore` (no `--no-build` — see the spike).
- Generated TypeScript whose generators need a build to run is **committed**
  and CI proves freshness: `src/lib/api-types.gen.ts` (renamed from
  `api-types.ts`, generated from the build-emitted OpenAPI document) and
  `src/routeTree.gen.ts` (TanStack Router, per upstream recommendation). CI
  regenerates both and fails on `git diff --exit-code -- '*.gen.ts'`. A
  `.gitattributes` rule marks `*.gen.ts` as `linguist-generated` so PR diffs
  collapse them. Docker does not run codegen; the committed file is the
  contract and the CI drift gate enforces it.
- CI order (single sequential job — codegen depends on `dotnet build`, which
  kills most parallelism a job split would buy): restore → CSharpier →
  `dotnet build` → `npm run codegen` → lint, format check, typecheck, vitest →
  `npm run build` → `.gen.ts` drift gate → `dotnet test`.

## Consequences

- `dotnet run` serves whatever SPA build last landed in `wwwroot/` (possibly
  nothing on a fresh clone). The dev front door remains the Vite dev server.
- Backend iteration no longer pays the Vite + `tsc` cost on every
  `dotnet build`.
- Frontend-only work (including agent sandboxes) needs no .NET build to
  typecheck, lint, or test — the committed `.gen.ts` files carry the contract.
- The repo depends only on the paved-road `wwwroot` discovery path of the
  static-web-assets SDK; no Visual Studio JavaScript SDK, no custom
  static-web-assets MSBuild.
- Regenerating after a backend API change during local development is a
  manual `npm run codegen` for now; automating that trigger is a known
  follow-up.
