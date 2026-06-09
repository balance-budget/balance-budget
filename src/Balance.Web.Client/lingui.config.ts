import { defineConfig } from '@lingui/cli';
import { formatter } from '@lingui/format-po';

// Catalog config (ADR-0022). Message IDs are generated from the English source
// text, so `en` is both the source locale and an implicit catalog. The PO files
// under src/locales are the drift-gated artifact; `npm run extract` rewrites
// them and CI fails if they differ from what the source produces.
export default defineConfig({
    sourceLocale: 'en',
    locales: ['en'],
    catalogs: [
        {
            path: '<rootDir>/src/locales/{locale}/messages',
            include: ['src'],
        },
    ],
    // Drop source-line `#:` references so the drift-gated catalog stays stable when
    // code moves around; only message changes touch the PO files (ADR-0022).
    format: formatter({ origins: false }),
});
