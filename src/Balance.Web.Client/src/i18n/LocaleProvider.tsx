/*
 * LocaleProvider (ADR-0022). Resolves the signed-in user's display preferences
 * (from /me, defaults when logged out or unset) and applies them to the three
 * surfaces that need a locale:
 *   - the formatting singleton in format.ts (dates/numbers/money),
 *   - React Aria's I18nProvider (editable widgets + RAC's own labels),
 *   - Lingui's I18nProvider (translated strings).
 *
 * Browser locale is intentionally NOT consulted — the default is ISO, and the
 * user's saved choice always wins (the requirement that drove this work).
 */

import { useContext, useLayoutEffect, createContext, type ReactNode } from 'react';
import { I18nProvider as AriaI18nProvider } from 'react-aria-components';
import { I18nProvider as LinguiProvider } from '@lingui/react';
import { useCurrentUser } from '../api/auth';
import {
    activateLanguage,
    DEFAULT_LANGUAGE,
    i18n,
    isSupportedLanguage,
    type Language,
} from './i18n';
import { backingTag, DEFAULT_REGION, resolveRegion, type RegionSettings } from './region';
import { setActiveLanguage, setActiveRegion } from './format';

type LocaleContextValue = {
    language: Language;
    region: RegionSettings;
};

const LocaleContext = createContext<LocaleContextValue>({
    language: DEFAULT_LANGUAGE,
    region: DEFAULT_REGION,
});

// eslint-disable-next-line react-refresh/only-export-components -- the provider and its hook belong together; HMR cost is negligible for a top-level provider.
export function useLocaleSettings(): LocaleContextValue {
    return useContext(LocaleContext);
}

export function LocaleProvider({ children }: { children: ReactNode }) {
    const { data: user } = useCurrentUser();
    const language: Language = isSupportedLanguage(user?.language)
        ? user.language
        : DEFAULT_LANGUAGE;
    const region = resolveRegion(user?.dateFormat, user?.numberFormat);

    // Sync the plain formatting singletons before children render so the pure
    // formatters in lib/ read fresh values on this pass. These are module-level
    // variable writes with no React state, so they're safe during render.
    setActiveRegion(region);
    setActiveLanguage(language);

    // Activating the Lingui catalog emits a change event that makes Lingui's
    // I18nProvider call setState. Doing that during render triggers React's
    // "Cannot update a component while rendering a different component" warning,
    // so defer it to a layout effect. It runs before paint, so Lingui re-renders
    // with the new catalog synchronously and the user never sees a stale pass.
    useLayoutEffect(() => {
        if (i18n.locale !== language) {
            activateLanguage(language);
        }
    }, [language]);

    // Remount the subtree on a (rare, user-initiated) settings change so chart and
    // money formatters that read the singleton recompute. `display: contents`
    // keeps the wrapper layout-neutral.
    const localeKey = `${language}|${region.dateFormat}|${region.numberFormat}`;

    return (
        <LocaleContext value={{ language, region }}>
            <AriaI18nProvider locale={backingTag(language, region)}>
                <LinguiProvider i18n={i18n}>
                    <div key={localeKey} className="contents">
                        {children}
                    </div>
                </LinguiProvider>
            </AriaI18nProvider>
        </LocaleContext>
    );
}
