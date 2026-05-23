# Balance.Web.Client

React + Vite SPA shipped inside `Balance.Web`'s publish output. The full project
overview (architecture, conventions, run instructions) lives in the repository
root — see [`CLAUDE.md`](../../CLAUDE.md) and [`docs/`](../../docs).

## Local development

From this folder:

```bash
npm install
npm run dev        # Vite dev server on http://localhost:5173, proxies /api → http://localhost:5248
npm run build      # vite build && tsc -b
npm run lint       # eslint
npm run format     # prettier --write
npm run codegen    # regenerate src/lib/api-types.ts from the .NET OpenAPI doc
```

The .NET host needs to be running for `/api/*` requests; from the repo root:

```bash
dotnet run --project src/Balance.Web/Balance.Web.csproj
```
