/*
 * Region formatting model (ADR-0029, superseding the date/number stance of
 * ADR-0022). Two symmetric per-user preferences, each a simple `locale | iso`
 * toggle:
 *
 *   - dateFormat   — `locale` defers date order, month names, and separators to
 *                    the user's `language`; `iso` forces numeric `YYYY-MM-DD`.
 *   - numberFormat — `locale` defers grouping + decimal mark to `language`;
 *                    `iso` forces ISO 80000 style `1 234.56` (narrow no-break
 *                    space groups, dot decimal).
 *
 * Display formatting lives in `format.ts`. React Aria's editable widgets take a
 * single backing locale (`backingTag`); per the user's call, those widgets just
 * follow the culture — the backing tag honors the date order (the focus of this
 * work) and lets number separators fall out of that one locale.
 */

export type DateFormatPref = 'locale' | 'iso';
export type NumberFormatPref = 'locale' | 'iso';

export type RegionSettings = {
    dateFormat: DateFormatPref;
    numberFormat: NumberFormatPref;
};

export const DATE_FORMATS = ['locale', 'iso'] as const satisfies readonly DateFormatPref[];
export const NUMBER_FORMATS = ['locale', 'iso'] as const satisfies readonly NumberFormatPref[];

// ISO is the default — unambiguous, and the escape hatch for anyone unhappy with
// their locale's native order (ADR-0029).
export const DEFAULT_REGION: RegionSettings = { dateFormat: 'iso', numberFormat: 'iso' };

// Coerce persisted tokens, including the retired ADR-0022 vocabulary, to the
// current pair. The old order choices (`dmy`/`mdy`) and number styles map to
// `locale` so users who picked a non-ISO format keep "follow my language"
// rather than silently flipping to the ISO default.
const LEGACY_DATE: Record<string, DateFormatPref> = {
    iso: 'iso',
    locale: 'locale',
    dmy: 'locale',
    mdy: 'locale',
};

const LEGACY_NUMBER: Record<string, NumberFormatPref> = {
    iso: 'iso',
    locale: 'locale',
    'comma-dot': 'locale',
    'dot-comma': 'locale',
    'space-comma': 'locale',
};

export function isDateFormat(value: unknown): value is DateFormatPref {
    return typeof value === 'string' && (DATE_FORMATS as readonly string[]).includes(value);
}

export function isNumberFormat(value: unknown): value is NumberFormatPref {
    return typeof value === 'string' && (NUMBER_FORMATS as readonly string[]).includes(value);
}

/**
 * The single BCP-47 tag fed to React Aria's I18nProvider for editable widgets.
 * ISO dates need `en-CA`'s numeric `yyyy-mm-dd` segments; otherwise the user's
 * language drives both date-segment order and RAC's own labels. Number
 * separators in editable fields follow whatever that one tag implies.
 */
export function backingTag(language: string, region: RegionSettings): string {
    return region.dateFormat === 'iso' ? 'en-CA' : language;
}

function mapToken<T>(value: string | null | undefined, table: Record<string, T>, fallback: T): T {
    if (value == null) return fallback;
    return table[value] ?? fallback;
}

/** Coerce persisted (possibly null/unknown/legacy) preference tokens to a valid region. */
export function resolveRegion(
    dateFormat: string | null | undefined,
    numberFormat: string | null | undefined,
): RegionSettings {
    return {
        dateFormat: mapToken(dateFormat, LEGACY_DATE, DEFAULT_REGION.dateFormat),
        numberFormat: mapToken(numberFormat, LEGACY_NUMBER, DEFAULT_REGION.numberFormat),
    };
}
