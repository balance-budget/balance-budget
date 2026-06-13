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
import type { RegisterSummaryBucketSize } from './registerSummary';

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
    if (range === '1Y') {
        return formatMonthAxisDate(date);
    }
    return formatDate(date, { month: 'short' });
}

/**
 * Shared month-bucketed x-axis tick: a short month name, with the full
 * (4-digit) year on January to mark the boundary. One formatter so every
 * monthly axis — the trend chart, the register summary, and the Outlook
 * projection — reads identically (and stays region-aware via `formatDate`).
 */
export function formatMonthAxisDate(date: string): string {
    return parseIsoDate(date).getMonth() === 0
        ? formatDate(date, { month: 'short', year: 'numeric' })
        : formatDate(date, { month: 'short' });
}

/**
 * Bucket-aware x-axis tick for the Register summary chart: monthly buckets
 * read as month names (year suffix on January to mark the boundary), daily
 * and weekly buckets keep the day-of-month.
 */
export function formatBucketAxisDate(date: string, bucket: RegisterSummaryBucketSize): string {
    if (bucket === 'Month') {
        return formatMonthAxisDate(date);
    }
    return formatDate(date, { month: 'short', day: 'numeric' });
}

/** Bucket-aware tooltip heading: "May 2026" for a month, a full date otherwise. */
export function formatBucketTooltipDate(date: string, bucket: RegisterSummaryBucketSize): string {
    if (bucket === 'Month') {
        return formatDate(date, { month: 'long', year: 'numeric' });
    }
    return formatDate(date, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' });
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

/** A calendar date for list/table rows and detail headers, e.g. "12 Jun 2026"
 *  in region order. The single place a `YYYY-MM-DD` becomes a human date in a
 *  table, so every list reads the same and follows the region (ADR-0022)
 *  instead of showing a raw ISO string. */
export function formatTableDate(date: string): string {
    return formatDate(date, { year: 'numeric', month: 'short', day: 'numeric' });
}

/** A month-and-year label for schedule/amortization rows, e.g. "2026-01" (ISO),
 *  "01/2026" (day-first / month-first). Numeric on purpose: a *named* month
 *  ("Jan") renders identically across the en-CA/en-GB/en-US locales we map the
 *  date preference to, so it would silently ignore that preference — only the
 *  numeric form actually follows it. Takes any `YYYY-MM…` period string. */
export function formatScheduleMonth(period: string): string {
    return formatDate(`${period.slice(0, 7)}-01`, { year: 'numeric', month: '2-digit' });
}
