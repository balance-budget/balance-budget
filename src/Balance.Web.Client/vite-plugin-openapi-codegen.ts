import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import type { Plugin } from 'vite';

// The OpenAPI document `dotnet build` emits (see Balance.Web.csproj). Keep in sync
// with the `codegen` npm script, which reads the same path.
const openApiDocument = path.resolve(
    import.meta.dirname,
    '../../artifacts/openapi/Balance.Web.json',
);

/**
 * Dev-server-only plugin: watches the build-emitted OpenAPI document and re-runs
 * `npm run codegen` when its content changes, so `src/lib/api-types.gen.ts` tracks
 * the backend API surface without a manual step (ADR-0023 follow-up).
 *
 * It deliberately spawns the npm script instead of calling the openapi-typescript
 * Node API: the committed output must stay byte-identical to what CI's drift gate
 * regenerates with the CLI, and one shared entry point makes that definitional.
 *
 * A content hash guards against no-op runs — `dotnet build` rewrites the document
 * on every build even when the API surface is unchanged.
 */
export function openApiCodegen(): Plugin {
    let lastHash: string | null = null;
    let debounce: ReturnType<typeof setTimeout> | undefined;

    const hashDocument = (): string | null =>
        existsSync(openApiDocument)
            ? createHash('sha256').update(readFileSync(openApiDocument)).digest('hex')
            : null;

    return {
        name: 'balance:openapi-codegen',
        apply: 'serve',
        configureServer(server) {
            lastHash = hashDocument();
            server.watcher.add(openApiDocument);

            const onChange = (file: string): void => {
                if (path.resolve(file) !== openApiDocument) return;

                // Let the .NET build finish writing before reading.
                clearTimeout(debounce);
                debounce = setTimeout(() => {
                    const hash = hashDocument();
                    if (hash === null || hash === lastHash) return;

                    lastHash = hash;
                    server.config.logger.info(
                        'OpenAPI document changed — regenerating api-types.gen.ts',
                        { timestamp: true },
                    );
                    spawn('npm', ['run', 'codegen'], {
                        cwd: import.meta.dirname,
                        stdio: 'inherit',
                    }).on('exit', code => {
                        if (code === 0) return;

                        // Retry on the next change, even if the content is identical.
                        lastHash = null;
                        server.config.logger.error(`codegen exited with code ${String(code)}`, {
                            timestamp: true,
                        });
                    });
                }, 200);
            };

            server.watcher.on('add', onChange);
            server.watcher.on('change', onChange);
        },
    };
}
