/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import { lingui } from '@lingui/vite-plugin';
import { openApiCodegen } from './vite-plugin-openapi-codegen.ts';

export default defineConfig({
    plugins: [
        openApiCodegen(),
        tanstackRouter({ target: 'react', autoCodeSplitting: true }),
        // plugin-react v6 transforms via oxc and has no Babel hook, so the Lingui
        // macros (<Trans>, t``) are compiled by the SWC plugin here; @lingui/vite-plugin
        // compiles `.po` catalogs to message objects on import (ADR-0022).
        react({ plugins: [['@lingui/swc-plugin', {}]] }),
        lingui(),
        tailwindcss(),
    ],
    build: {
        // Build straight into the ASP.NET host's web root so the standard
        // static-web-assets discovery pipeline picks the SPA up on publish
        // and MapStaticAssets() serves it. See ADR-0023.
        outDir: '../Balance.Web/wwwroot',
        emptyOutDir: true,
        sourcemap: true,
    },
    server: {
        proxy: {
            '/api': {
                target: 'http://localhost:5248',
                changeOrigin: true,
                xfwd: true,
                secure: false,
            },
        },
    },
    test: {
        include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
        environment: 'node',
        setupFiles: ['src/test.setup.ts'],
    },
});
