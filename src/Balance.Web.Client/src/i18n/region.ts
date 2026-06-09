/*
 * Region formatting model (ADR-0022). Independent of language: the user picks a
 * date order and a number-grouping style, and these drive *formatting* only —
 * the UI text stays in whatever `language` is active.
 *
 * Display formatters (format.ts) honor the date and number preferences
 * independently. React Aria's editable widgets take a single locale, so
 * `backingTag` collapses the pair to the best-fit BCP-47 tag; exotic
 * combinations not expressible by one tag fall back to the date order.
 */

export type DateFormatPref = 'iso' | 'dmy' | 'mdy';
export type NumberFormatPref = 'comma-dot' | 'dot-comma' | 'space-comma';

export type RegionSettings = {
    dateFormat: DateFormatPref;
    numberFormat: NumberFormatPref;
};

export const DATE_FORMATS = ['iso', 'dmy', 'mdy'] as const satisfies readonly DateFormatPref[];
export const NUMBER_FORMATS = [
    'comma-dot',
    'dot-comma',
    'space-comma',
] as const satisfies readonly NumberFormatPref[];

export const DEFAULT_REGION: RegionSettings = { dateFormat: 'iso', numberFormat: 'comma-dot' };

// Date-display locales — all English (month names stay English); only the field
// order differs. ISO → en-CA renders yyyy-mm-dd numerically.
const DATE_LOCALE: Record<DateFormatPref, string> = {
    iso: 'en-CA', // 2026-03-09
    dmy: 'en-GB', // 09/03/2026
    mdy: 'en-US', // 3/9/2026
};

// Number-display locales, picked for their grouping + decimal separators.
const NUMBER_LOCALE: Record<NumberFormatPref, string> = {
    'comma-dot': 'en-US', // 1,234.56
    'dot-comma': 'nl-NL', // 1.234,56
    'space-comma': 'en-SE', // 1 234,56
};

const DECIMAL_SEPARATOR: Record<NumberFormatPref, '.' | ','> = {
    'comma-dot': '.',
    'dot-comma': ',',
    'space-comma': ',',
};

export function dateLocale(pref: DateFormatPref): string {
    return DATE_LOCALE[pref];
}

export function numberLocale(pref: NumberFormatPref): string {
    return NUMBER_LOCALE[pref];
}

export function decimalSeparator(pref: NumberFormatPref): '.' | ',' {
    return DECIMAL_SEPARATOR[pref];
}

/**
 * The single BCP-47 tag fed to React Aria's I18nProvider. Its editable widgets
 * derive date order *and* separators from one locale, so we honor the date order
 * first and match the number style only where a clean tag exists.
 */
export function backingTag(region: RegionSettings): string {
    const exact: Record<string, string> = {
        'iso/space-comma': 'en-SE',
        'dmy/dot-comma': 'en-NL',
    };
    return exact[`${region.dateFormat}/${region.numberFormat}`] ?? DATE_LOCALE[region.dateFormat];
}

export function isDateFormat(value: unknown): value is DateFormatPref {
    return typeof value === 'string' && (DATE_FORMATS as readonly string[]).includes(value);
}

export function isNumberFormat(value: unknown): value is NumberFormatPref {
    return typeof value === 'string' && (NUMBER_FORMATS as readonly string[]).includes(value);
}

/** Coerce persisted (possibly null/unknown) preference tokens to a valid region. */
export function resolveRegion(
    dateFormat: string | null | undefined,
    numberFormat: string | null | undefined,
): RegionSettings {
    return {
        dateFormat: isDateFormat(dateFormat) ? dateFormat : DEFAULT_REGION.dateFormat,
        numberFormat: isNumberFormat(numberFormat) ? numberFormat : DEFAULT_REGION.numberFormat,
    };
}
