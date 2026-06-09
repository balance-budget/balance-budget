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

import { useContext, createContext, type ReactNode } from 'react';
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
import { setActiveRegion } from './format';

type LocaleContextValue = {
    language: Language;
    region: RegionSettings;
};

const LocaleContext = createContext<LocaleContextValue>({
    language: DEFAULT_LANGUAGE,
    region: DEFAULT_REGION,
});

export function useLocaleSettings(): LocaleContextValue {
    return useContext(LocaleContext);
}

export function LocaleProvider({ children }: { children: ReactNode }) {
    const { data: user } = useCurrentUser();
    const language: Language = isSupportedLanguage(user?.language)
        ? user.language
        : DEFAULT_LANGUAGE;
    const region = resolveRegion(user?.dateFormat, user?.numberFormat);

    // Sync the singleton + Lingui catalog before children render so the pure
    // formatters in lib/ read fresh values on this pass.
    setActiveRegion(region);
    if (i18n.locale !== language) {
        activateLanguage(language);
    }

    // Remount the subtree on a (rare, user-initiated) settings change so chart and
    // money formatters that read the singleton recompute. `display: contents`
    // keeps the wrapper layout-neutral.
    const localeKey = `${language}|${region.dateFormat}|${region.numberFormat}`;

    return (
        <LocaleContext value={{ language, region }}>
            <AriaI18nProvider locale={backingTag(region)}>
                <LinguiProvider i18n={i18n}>
                    <div key={localeKey} className="contents">
                        {children}
                    </div>
                </LinguiProvider>
            </AriaI18nProvider>
        </LocaleContext>
    );
}
