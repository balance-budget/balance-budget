/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';

export default defineConfig({
    plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
    build: {
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
    },
});
