/*
 * Date formatters for charts. The chart axis cares about cadence (range
 * dictates whether the day-of-month is informative); tooltips want a full
 * human-readable date. Formatting goes through `i18n/format` so the order
 * follows the user's date preference (ADR-0022); parsing/arithmetic is
 * `@internationalized/date` (ADR-0024) — no hand-rolled ISO handling.
 */

import { getLocalTimeZone, today } from '@internationalized/date';
import type { TrendRange } from '../api/dashboard';
import { formatDate, parseIsoDate } from '../i18n/format';

/** Today's date as a local `YYYY-MM-DD` string (for date-input defaults). */
export function todayIso(): string {
    return today(getLocalTimeZone()).toString();
}

/**
 * Range-aware x-axis tick formatter. 1M gives weekly ticks where day-of-month
 * matters; 3M/6M run monthly so day-of-month is noise; 1Y is monthly with a
 * year suffix only on January, to disambiguate the boundary without crowding
 * every label.
 */
export function formatTrendAxisDate(date: string, range: TrendRange): string {
    if (range === '1M') {
        return formatDate(date, { month: 'short', day: 'numeric' });
    }
    if (range === '1Y' && parseIsoDate(date).getMonth() === 0) {
        return formatDate(date, { month: 'short', year: '2-digit' });
    }
    return formatDate(date, { month: 'short' });
}

/** Full tooltip date, e.g. "Tue, May 14, 2026". */
export function formatTrendTooltipDate(date: string): string {
    return formatDate(date, {
        weekday: 'short',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
    });
}
