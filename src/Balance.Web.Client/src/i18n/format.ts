/*
 * Date / number / money formatting (ADR-0022). The single source of truth for
 * *formatting*; Lingui handles language only. Formatters read an active region
 * singleton (kept in sync by LocaleProvider) so pure callers in lib/ — money.ts,
 * dates.ts — don't have to thread settings through every call site.
 *
 * Two date rules from the ADR:
 *   - Calendar dates (canonical ISO `YYYY-MM-DD`) are never timezone-converted.
 *   - Instants (UTC timestamps) render in the browser's local timezone; their
 *     *order* still follows the date preference, their *zone* does not.
 */

import { getLocalTimeZone, parseDate } from '@internationalized/date';
import {
    DEFAULT_REGION,
    dateLocale,
    decimalSeparator,
    numberLocale,
    type RegionSettings,
} from './region';

let activeRegion: RegionSettings = DEFAULT_REGION;

export function setActiveRegion(region: RegionSettings): void {
    activeRegion = region;
}

export function getActiveRegion(): RegionSettings {
    return activeRegion;
}

export function activeDateLocale(): string {
    return dateLocale(activeRegion.dateFormat);
}

export function activeNumberLocale(): string {
    return numberLocale(activeRegion.numberFormat);
}

export function activeDecimalSeparator(): '.' | ',' {
    return decimalSeparator(activeRegion.numberFormat);
}

// Intl formatters are costly to construct; cache by locale + options.
const dateFormatterCache = new Map<string, Intl.DateTimeFormat>();

function dateFormatter(options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
    const locale = activeDateLocale();
    const key = `${locale}|${JSON.stringify(options)}`;
    let formatter = dateFormatterCache.get(key);
    if (!formatter) {
        formatter = new Intl.DateTimeFormat(locale, options);
        dateFormatterCache.set(key, formatter);
    }
    return formatter;
}

const numberFormatterCache = new Map<string, Intl.NumberFormat>();

function numberFormatter(options: Intl.NumberFormatOptions): Intl.NumberFormat {
    const locale = activeNumberLocale();
    const key = `${locale}|${JSON.stringify(options)}`;
    let formatter = numberFormatterCache.get(key);
    if (!formatter) {
        formatter = new Intl.NumberFormat(locale, options);
        numberFormatterCache.set(key, formatter);
    }
    return formatter;
}

/** Parse a canonical ISO `YYYY-MM-DD` to a local Date with no UTC drift. */
export function parseIsoDate(iso: string): Date {
    return parseDate(iso).toDate(getLocalTimeZone());
}

/** Format a canonical ISO *calendar date* (no timezone involved). */
export function formatDate(iso: string, options: Intl.DateTimeFormatOptions): string {
    return dateFormatter(options).format(parseIsoDate(iso));
}

/**
 * Format a UTC *instant* (e.g. CreatedAt) in the browser's local timezone. The
 * field order follows the date preference; the zone is the runtime's local zone.
 */
export function formatInstant(isoTimestamp: string, options: Intl.DateTimeFormatOptions): string {
    return dateFormatter(options).format(new Date(isoTimestamp));
}

/** Format a number with the active grouping/decimal preference. */
export function formatNumber(value: number, options: Intl.NumberFormatOptions = {}): string {
    return numberFormatter(options).format(value);
}
