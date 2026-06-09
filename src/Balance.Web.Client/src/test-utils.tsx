/* eslint-disable react-refresh/only-export-components -- test helper module, not HMR-relevant. */
import type { ReactElement, ReactNode } from 'react';
import { render as rtlRender, type RenderOptions } from '@testing-library/react';
import { I18nProvider } from '@lingui/react';
import { i18n } from './i18n/i18n';

// Components wrapped with Lingui macros (<Trans>, useLingui) need the I18nProvider
// in the tree. Tests render through this wrapper so they don't each repeat it.
function Wrapper({ children }: { children: ReactNode }) {
    return <I18nProvider i18n={i18n}>{children}</I18nProvider>;
}

function render(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
    return rtlRender(ui, { wrapper: Wrapper, ...options });
}

export * from '@testing-library/react';
export { render };
