/*
 * Date / number / money formatting (ADR-0029). The single source of truth for
 * *formatting*; Lingui handles language only. This is the one module allowed to
 * construct `Intl.*` formatters or call `toLocale*` — a lint rule
 * (`no-restricted-syntax`) bans them everywhere else, so every user-facing date
 * and number flows through here and honors the live preference.
 *
 * Callers pass *intent* — a date granularity (+ short/long style, optional
 * weekday) or a plain number — never raw `Intl` option bags. The active
 * `language` + `region` singletons (kept fresh by LocaleProvider) let pure
 * callers in lib/ — money.ts, dates.ts — read settings without threading them
 * through every call site.
 *
 * Two rules from the ADR:
 *   - `locale` mode defers to the user's `language` locale; `iso` mode forces
 *     numeric ISO (dates) / ISO 80000 `1 234.56` (numbers), ignoring style.
 *   - Calendar dates (canonical ISO `YYYY-MM-DD`) are never timezone-converted;
 *     instants (UTC timestamps) render in the browser's local timezone.
 */

import { getLocalTimeZone, parseDate } from '@internationalized/date';
import { DEFAULT_LANGUAGE } from './i18n';
import { DEFAULT_REGION, type DateFormatPref, type NumberFormatPref } from './region';

let activeRegion = DEFAULT_REGION;
let activeLanguage: string = DEFAULT_LANGUAGE;

export function setActiveRegion(region: typeof DEFAULT_REGION): void {
    activeRegion = region;
}

export function setActiveLanguage(language: string): void {
    activeLanguage = language;
}

export function getActiveRegion(): typeof DEFAULT_REGION {
    return activeRegion;
}

// ── Dates ───────────────────────────────────────────────────────────────────

/** A semantic date shape. Order/month-name come from the locale (or ISO). */
export type DateGranularity = 'year' | 'month' | 'month-day' | 'year-month' | 'year-month-day';

export type CalendarDateOptions = {
    /** Month-name length when a day is *absent*; ignored when a day is present
     *  (the rubric: day ⇒ short month) and in ISO mode. Defaults to `short`. */
    style?: 'short' | 'long';
    /** Prepend a short weekday (chart tooltips only). Ignored in ISO mode. */
    weekday?: boolean;
};

// Intl formatters are costly to construct; cache by locale + options.
const dateFormatterCache = new Map<string, Intl.DateTimeFormat>();

function dateFormatter(locale: string, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
    const key = `${locale}|${JSON.stringify(options)}`;
    let formatter = dateFormatterCache.get(key);
    if (!formatter) {
        formatter = new Intl.DateTimeFormat(locale, options);
        dateFormatterCache.set(key, formatter);
    }
    return formatter;
}

/** Parse a canonical ISO `YYYY-MM-DD` to a local Date with no UTC drift. */
export function parseIsoDate(iso: string): Date {
    return parseDate(canonicalDate(iso)).toDate(getLocalTimeZone());
}

/** Normalize a `YYYY`, `YYYY-MM`, or longer ISO string to a full `YYYY-MM-DD`. */
function canonicalDate(iso: string): string {
    if (iso.length === 4) return `${iso}-01-01`;
    if (iso.length === 7) return `${iso}-01`;
    return iso.slice(0, 10);
}

function isoCalendar(iso: string, granularity: DateGranularity): string {
    const year = iso.slice(0, 4);
    const month = iso.slice(5, 7);
    const day = iso.slice(8, 10);
    switch (granularity) {
        case 'year':
            return year;
        case 'month':
            return month;
        case 'month-day':
            return `${month}-${day}`;
        case 'year-month':
            return `${year}-${month}`;
        case 'year-month-day':
            return `${year}-${month}-${day}`;
    }
}

function calendarOptions(
    granularity: DateGranularity,
    options: CalendarDateOptions,
): Intl.DateTimeFormatOptions {
    const hasYear =
        granularity === 'year' || granularity === 'year-month' || granularity === 'year-month-day';
    const hasMonth = granularity !== 'year';
    const hasDay = granularity === 'month-day' || granularity === 'year-month-day';
    // The rubric: a day present forces a short month; only month+year headings go long.
    const monthStyle = hasDay ? 'short' : (options.style ?? 'short');
    return {
        ...(hasYear && { year: 'numeric' }),
        ...(hasMonth && { month: monthStyle }),
        ...(hasDay && { day: 'numeric' }),
        ...(options.weekday && { weekday: 'short' }),
    };
}

/** Format a canonical ISO calendar date with an explicit language + preference. */
export function formatCalendarDateWith(
    language: string,
    dateFormat: DateFormatPref,
    iso: string,
    granularity: DateGranularity,
    options: CalendarDateOptions = {},
): string {
    if (dateFormat === 'iso') return isoCalendar(iso, granularity);
    return dateFormatter(language, calendarOptions(granularity, options)).format(parseIsoDate(iso));
}

/** Format a canonical ISO calendar date (no timezone) per the active preference. */
export function formatCalendarDate(
    iso: string,
    granularity: DateGranularity,
    options: CalendarDateOptions = {},
): string {
    return formatCalendarDateWith(
        activeLanguage,
        activeRegion.dateFormat,
        iso,
        granularity,
        options,
    );
}

/**
 * Format a UTC *instant* (e.g. CreatedAt) in the browser's local timezone as a
 * short date plus time. ISO mode renders `2026-06-13 14:30` (24h); locale mode
 * uses the language's own date order and clock.
 */
export function formatInstant(isoTimestamp: string): string {
    const instant = new Date(isoTimestamp);
    if (activeRegion.dateFormat === 'iso') {
        const date = dateFormatter('en-CA', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
        }).format(instant);
        const time = dateFormatter('en-GB', {
            hour: '2-digit',
            minute: '2-digit',
            hourCycle: 'h23',
        }).format(instant);
        return `${date} ${time}`;
    }
    return dateFormatter(activeLanguage, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    }).format(instant);
}

// ── Numbers ───────────────────────────────────────────────────────────────────

// ISO 80000: narrow no-break space groups, dot decimal. Built from en-US parts
// (comma group, dot decimal) by swapping the group separator.
const ISO_GROUP_SEPARATOR = '\u202F';

function numberToString(
    language: string,
    numberFormat: NumberFormatPref,
    value: number,
    options: Intl.NumberFormatOptions,
): string {
    if (numberFormat === 'iso') {
        return new Intl.NumberFormat('en-US', { useGrouping: true, ...options })
            .formatToParts(value)
            .map(part => (part.type === 'group' ? ISO_GROUP_SEPARATOR : part.value))
            .join('');
    }
    return new Intl.NumberFormat(language, options).format(value);
}

/** Format a number with the active grouping/decimal preference. */
export function formatNumber(value: number, options: Intl.NumberFormatOptions = {}): string {
    return numberToString(activeLanguage, activeRegion.numberFormat, value, options);
}

/** Group an integer per the active number preference (no fractional digits). */
export function groupInteger(value: number): string {
    return numberToString(activeLanguage, activeRegion.numberFormat, value, {
        maximumFractionDigits: 0,
    });
}

/** The decimal mark for the active number preference — `.` in ISO mode. */
export function activeDecimalSeparator(): '.' | ',' {
    if (activeRegion.numberFormat === 'iso') return '.';
    const decimal = new Intl.NumberFormat(activeLanguage)
        .formatToParts(1.1)
        .find(part => part.type === 'decimal')?.value;
    return decimal === ',' ? ',' : '.';
}

// ── Previews (settings UI) ──────────────────────────────────────────────────

const PREVIEW_DATE = '2026-03-09';
const PREVIEW_NUMBER = 1234567.89;

/** Sample date for a date-format option, in a given language. */
export function previewDate(language: string, dateFormat: DateFormatPref): string {
    return formatCalendarDateWith(language, dateFormat, PREVIEW_DATE, 'year-month-day');
}

/** Sample number for a number-format option, in a given language. */
export function previewNumber(language: string, numberFormat: NumberFormatPref): string {
    return numberToString(language, numberFormat, PREVIEW_NUMBER, {});
}
