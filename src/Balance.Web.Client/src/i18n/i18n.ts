/*
 * Lingui i18n instance (ADR-0022). Lingui owns *language* only — all date,
 * number, and money formatting lives in `format.ts` and is driven by the
 * separate region setting. Message IDs are generated from English source text,
 * so an un-translated string still renders its source default.
 *
 * Catalogs are statically imported so `activateLanguage` stays synchronous:
 * the LocaleProvider can swap the catalog during render without a loading gap.
 * React Aria's own component labels translate themselves for 30+ locales.
 */

import { i18n } from '@lingui/core';
import { messages as enMessages } from '../locales/en/messages.po';
import { messages as nlMessages } from '../locales/nl-NL/messages.po';
import { messages as zhTwMessages } from '../locales/zh-TW/messages.po';

export const SUPPORTED_LANGUAGES = ['en', 'nl-NL', 'zh-TW'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'en';

// Human-readable names, shown each in its own language so a user can find their
// own. Keep in sync with SUPPORTED_LANGUAGES.
export const LANGUAGE_NAMES: Record<Language, string> = {
    en: 'English',
    'nl-NL': 'Nederlands',
    'zh-TW': '繁體中文',
};

const CATALOGS: Record<Language, typeof enMessages> = {
    en: enMessages,
    'nl-NL': nlMessages,
    'zh-TW': zhTwMessages,
};

export function isSupportedLanguage(value: string | null | undefined): value is Language {
    return value != null && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/** Load + activate a language catalog. Synchronous while `en` is the only one. */
export function activateLanguage(language: Language): void {
    i18n.load(language, CATALOGS[language]);
    i18n.activate(language);
}

// Activate the default immediately so non-React code (and tests) always have an
// active locale; the LocaleProvider re-activates from the user's preference.
activateLanguage(DEFAULT_LANGUAGE);

export { i18n };
