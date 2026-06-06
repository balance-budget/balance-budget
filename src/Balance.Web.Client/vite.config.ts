/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';

export default defineConfig({
    plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
    build: {
        sourcemap: true,
        rollupOptions: {
            output: {
                advancedChunks: {
                    // Keep every es-toolkit module in a single chunk. Rolldown
                    // mis-links its lazily-initialised CommonJS factories when
                    // they are shared across split chunks (recharts core vs the
                    // Sankey in the reports chunk), emitting a self-referential
                    // `var n = n()` that throws "n is not a function" at runtime.
                    // See https://github.com/rolldown/rolldown — reproduced with
                    // rolldown 1.0.3 and 1.1.0; grouping avoids the cross-chunk
                    // factory references entirely.
                    groups: [{ name: 'es-toolkit', test: /node_modules\/es-toolkit\// }],
                },
            },
        },
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
    },
});
