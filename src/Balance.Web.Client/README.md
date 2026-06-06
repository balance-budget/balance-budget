# Balance.Web.Client

React + Vite SPA shipped inside `Balance.Web`'s publish output. The full project
overview (architecture, conventions, run instructions) lives in the repository
root — see [`CLAUDE.md`](../../CLAUDE.md) and [`docs/`](../../docs).

## Local development

This package is an [npm workspace](https://docs.npmjs.com/cli/v10/using-npm/workspaces)
declared in the root `package.json`, so dependencies live in a single root
`node_modules/` and you can run scripts from either the repo root or this
folder. Pick whichever you prefer.

From the repo root (recommended — there are wrapper scripts in the root
`package.json`):

```bash
npm install        # installs deps for the whole workspace
npm run dev        # Vite dev server on http://localhost:5173, proxies /api → http://localhost:5248
npm run build      # vite build && tsc -b
npm run lint       # eslint
npm run format     # prettier --write
npm run codegen    # regenerate src/lib/api-types.ts from the .NET OpenAPI doc
```

From this folder, the same scripts work directly:

```bash
npm install                # writes to the root node_modules (workspaces are detected automatically)
npm run dev
npm run build
# ...
```

For any script that the root doesn't forward, use `npm run <script> -w balance`
from the root (the `balance` here is the `name` in this `package.json`).

The .NET host needs to be running for `/api/*` requests; from the repo root:

```bash
dotnet run --project src/Balance.Web/Balance.Web.csproj
```
