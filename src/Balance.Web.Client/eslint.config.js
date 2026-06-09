import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import lingui from 'eslint-plugin-lingui';
import tseslint from 'typescript-eslint';
import { defineConfig, globalIgnores } from 'eslint/config';

export default defineConfig([
    globalIgnores(['dist', '**/*.gen.ts', 'src/locales/**']),
    {
        files: ['**/*.{ts,tsx}'],
        extends: [
            js.configs.recommended,
            tseslint.configs.strictTypeChecked,
            tseslint.configs.stylisticTypeChecked,
            reactHooks.configs.flat.recommended,
            reactRefresh.configs.vite,
        ],
        plugins: { lingui },
        languageOptions: {
            globals: globals.browser,
            parserOptions: {
                project: ['./tsconfig.app.json', './tsconfig.node.json'],
                tsconfigRootDir: import.meta.dirname,
            },
        },
        rules: {
            // The codebase uses `type X = { ... }` everywhere; that's the convention.
            '@typescript-eslint/consistent-type-definitions': ['error', 'type'],
            // ${response.status}, ${count}, etc. read fine.
            '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
            // No untranslated UI strings (ADR-0022). Starts as a warning while the
            // allowlist is tuned; promoted to error once the migration lands.
            'lingui/no-unlocalized-strings': [
                'warn',
                {
                    // Short lowercase tokens (tailwind classes, format keys, icon
                    // names) are never user-facing prose.
                    ignore: ['^[a-z0-9-]+$'],
                    ignoreFunctions: ['cx', 'cva', 'clsx', 'twMerge', 'console.*'],
                },
            ],
            'lingui/t-call-in-function': 'error',
            'lingui/no-single-variables-to-translate': 'warn',
        },
    },
    // Tests, build config, and non-rendering library/API modules carry no UI copy.
    {
        files: [
            '**/*.test.{ts,tsx}',
            '**/*.config.{ts,js}',
            'src/test.setup.ts',
            'src/lib/**',
            'src/api/**',
        ],
        rules: {
            'lingui/no-unlocalized-strings': 'off',
        },
    },
]);
