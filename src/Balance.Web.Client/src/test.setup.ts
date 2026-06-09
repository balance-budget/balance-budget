/*
 * Vitest setup. React Aria's collection utilities use `CSS.escape`, which
 * jsdom doesn't implement — install a spec-compliant-enough polyfill (after
 * https://drafts.csswg.org/cssom/#serialize-an-identifier) for the test
 * environment. Browsers all have it natively.
 */

import { afterEach } from 'vitest';

// Activate the i18n catalog so <Trans>/`t` render their English source defaults
// in tests (ADR-0022). Importing for its activation side effect.
import './i18n/i18n';

function cssEscape(value: string): string {
    let result = '';
    for (let i = 0; i < value.length; i++) {
        const char = value.charAt(i);
        const code = value.charCodeAt(i);
        if (code === 0) {
            result += '�';
        } else if (
            (code >= 0x30 && code <= 0x39 && i === 0) ||
            (code >= 0x30 && code <= 0x39 && i === 1 && value.charCodeAt(0) === 0x2d)
        ) {
            result += `\\${code.toString(16)} `;
        } else if (
            code >= 0x80 ||
            char === '-' ||
            char === '_' ||
            (code >= 0x30 && code <= 0x39) ||
            (code >= 0x41 && code <= 0x5a) ||
            (code >= 0x61 && code <= 0x7a)
        ) {
            result += char;
        } else {
            result += `\\${char}`;
        }
    }
    return result;
}

const globalWithCss = globalThis as { CSS?: { escape: (value: string) => string } };
globalWithCss.CSS ??= { escape: cssEscape };

// Vitest runs without injected globals, so React Testing Library can't hook
// its own afterEach — unmount rendered trees between tests explicitly.
afterEach(async () => {
    const { cleanup } = await import('@testing-library/react');
    cleanup();
});
