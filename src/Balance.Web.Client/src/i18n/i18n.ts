/*
 * Lingui i18n instance (ADR-0022). Lingui owns *language* only — all date,
 * number, and money formatting lives in `format.ts` and is driven by the
 * separate region setting. Message IDs are generated from English source text,
 * so an un-translated string still renders its source default.
 *
 * Only `en` ships today; adding `nl` is a second catalog plus a language option
 * (and React Aria's own component labels translate themselves for 30+ locales).
 */

import { i18n } from '@lingui/core';
import { messages as enMessages } from '../locales/en/messages.po';

export const SUPPORTED_LANGUAGES = ['en'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'en';

const CATALOGS: Record<Language, typeof enMessages> = {
    en: enMessages,
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
