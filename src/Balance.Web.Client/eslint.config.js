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
            // Every user-facing date/number must flow through src/i18n/format.ts so
            // it honors the per-user date/number preference (ADR-0029). Constructing
            // Intl formatters or calling toLocale*String anywhere else silently
            // bypasses that preference. format.ts and tests are exempt below.
            'no-restricted-syntax': [
                'error',
                {
                    selector:
                        "NewExpression[callee.object.name='Intl'][callee.property.name=/^(DateTimeFormat|NumberFormat)$/]",
                    message:
                        'Construct Intl formatters only in src/i18n/format.ts. Use formatCalendarDate / formatInstant / formatNumber so the user date/number preference is honored (ADR-0029).',
                },
                {
                    selector: "CallExpression[callee.property.name=/^toLocale(Date|Time)?String$/]",
                    message:
                        'Do not use toLocale*String — it bypasses the user date/number preference (ADR-0029). Use the helpers in src/i18n/format.ts.',
                },
            ],
            // No untranslated UI strings (ADR-0022). Enforced as an error: every
            // user-facing string must go through Lingui. The options below skip
            // things that are provably not prose.
            'lingui/no-unlocalized-strings': [
                'error',
                {
                    // Use TS types to skip string-literal unions (e.g. AccountType,
                    // RegisterStatusFilter) used as logic values, not copy.
                    useTsTypes: true,
                    ignore: [
                        // No letters at all: separators, punctuation, symbols, numbers.
                        '^[^A-Za-z]*$',
                        // A bare lowercase camelCase/kebab/dotted token is an
                        // identifier or enum value, never display copy (prose is
                        // capitalised or multi-word): 'ghost', 'check-circle',
                        // 'balance.sidebar.expanded-accounts'.
                        '^[a-z][a-zA-Z0-9.-]*$',
                        // Dunder sentinel keys: '__none__', '__create__'.
                        '^__.*__$',
                        // ALL-CAPS / const-style tokens: 'EUR', '3M', 'USD'.
                        '^[A-Z0-9_]+$',
                        // Dotted identifier paths: 'newCounterparty.name', 'simple.to'.
                        '^[A-Za-z][\\w]*(\\.[\\w]+)+$',
                        // Template literals with interpolation are keys / ids / URLs
                        // (real interpolated copy goes through <Trans>/t, never a raw string).
                        '\\$\\{',
                        // Field-path key fragments contain square brackets: 'lines[', '].amount'.
                        '[\\[\\]]',
                        // Keyboard keycaps.
                        '^(⌘K|Ctrl K|Esc)$',
                        // Product wordmark.
                        '^Balance$',
                        // CSS custom-property references in inline styles.
                        'var\\(',
                        // Tailwind / CSS utility class lists (a known utility prefix
                        // followed by - / : / [, or a data-[...] variant).
                        '(^|\\s)(bg|text|border|rounded|flex|grid|gap|p[xytblr]?|m[xytblr]?|w|h|min|max|size|outline|ring|shrink|grow|items|justify|self|absolute|relative|fixed|sticky|inline|block|hidden|font|opacity|cursor|select|transition|duration|ease|z|top|bottom|left|right|overflow|truncate|whitespace|leading|tracking|object|aspect|order|col|row|group|peer)[-:[]',
                        'data-\\[',
                    ],
                    // Attribute / prop / property names whose string values are
                    // identifiers, routes, class names, enums, or chart/style keys —
                    // never copy.
                    ignoreNames: [
                        'className',
                        'inputClassName',
                        'class',
                        'to',
                        'name',
                        'id',
                        'htmlFor',
                        'slot',
                        'type',
                        'role',
                        'key',
                        'aria-hidden',
                        'dataKey',
                        'nameKey',
                        'fill',
                        'stroke',
                        'color',
                        'style',
                        'border',
                        'background',
                        'domain',
                        'acceptedFileTypes',
                        'autoComplete',
                        'inputMode',
                        'iconName',
                        'icon',
                        // Component prop enums (string unions not always resolved by useTsTypes).
                        'variant',
                        'size',
                        'width',
                        'height',
                        'padding',
                        'placement',
                        'position',
                        'anchor',
                        'align',
                        'side',
                        'selectionMode',
                        'selectionBehavior',
                        'weekdayStyle',
                        'stackId',
                        'tone',
                        'mode',
                        'direction',
                        'granularity',
                        'level',
                        'status',
                        'value',
                        // Intl format-option tokens ('2-digit', 'numeric', …).
                        'month',
                        'day',
                        'year',
                        'weekday',
                    ],
                    ignoreFunctions: [
                        'cx',
                        'cva',
                        'clsx',
                        'twMerge',
                        'console.*',
                        'addEventListener',
                        'removeEventListener',
                        // Route path is the first arg, not copy.
                        'createFileRoute',
                        // HTTP helpers: URL + an internal operation label, never UI copy.
                        'getJson',
                        'postJson',
                        'postJsonNoContent',
                        'putJson',
                        'putJsonNoContent',
                        'patchJson',
                        'patchJsonBody',
                        'deleteJson',
                        'deleteRequest',
                        'postFormData',
                        // Developer diagnostics, never shown translated.
                        'Error',
                    ],
                },
            ],
            'lingui/t-call-in-function': 'error',
            'lingui/no-single-variables-to-translate': 'error',
        },
    },
    // Tests, build config/plugins, the Tailwind style helpers, and non-rendering
    // library/API modules carry no UI copy.
    {
        files: [
            '**/*.test.{ts,tsx}',
            '**/*.config.{ts,js}',
            'vite-plugin-*.ts',
            'src/test.setup.ts',
            'src/lib/**',
            'src/api/**',
            'src/i18n/**',
            'src/components/ui/styles.ts',
        ],
        rules: {
            'lingui/no-unlocalized-strings': 'off',
        },
    },
    // The one module allowed to construct Intl formatters / call toLocale* — it
    // IS the formatting layer (ADR-0029). Tests may also format directly to build
    // expected values.
    {
        files: ['src/i18n/format.ts', '**/*.test.{ts,tsx}'],
        rules: {
            'no-restricted-syntax': 'off',
        },
    },
]);
