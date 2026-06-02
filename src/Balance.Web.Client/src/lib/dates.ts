/*
 * Date formatters for charts. The chart axis cares about cadence (range
 * dictates whether the day-of-month is informative); tooltips want a full
 * human-readable date. Locale follows the browser's preference via
 * `Intl.DateTimeFormat(undefined, ...)`.
 */

import type { TrendRange } from '../api/dashboard';

/** Today's date as a local `YYYY-MM-DD` string (for date-input defaults). */
export function todayIso(): string {
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, '0');
    const d = String(now.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
}

const MONTH_SHORT = new Intl.DateTimeFormat(undefined, { month: 'short' });
const MONTH_DAY_SHORT = new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
});
const MONTH_YEAR_SHORT = new Intl.DateTimeFormat(undefined, {
    month: 'short',
    year: '2-digit',
});
const TOOLTIP_DATE = new Intl.DateTimeFormat(undefined, {
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
});

/**
 * Range-aware x-axis tick formatter. 1M gives weekly ticks where day-of-month
 * matters; 3M/6M run monthly so day-of-month is noise; 1Y is monthly with a
 * year suffix only on January, to disambiguate the boundary without crowding
 * every label.
 */
export function formatTrendAxisDate(date: string, range: TrendRange): string {
    const parsed = parseIsoDate(date);
    if (range === '1M') {
        return MONTH_DAY_SHORT.format(parsed);
    }
    if (range === '1Y' && parsed.getMonth() === 0) {
        return MONTH_YEAR_SHORT.format(parsed);
    }
    return MONTH_SHORT.format(parsed);
}

/** Full tooltip date, e.g. "Tue, May 14, 2026". */
export function formatTrendTooltipDate(date: string): string {
    return TOOLTIP_DATE.format(parseIsoDate(date));
}

function parseIsoDate(date: string): Date {
    // The wire emits ISO `YYYY-MM-DD` from DateOnly. `new Date('YYYY-MM-DD')`
    // parses as UTC midnight, which can drift a day in negative-UTC locales;
    // construct from parts to keep the local calendar day stable.
    const [year = 1970, month = 1, day = 1] = date.split('-').map(Number);
    return new Date(year, month - 1, day);
}
