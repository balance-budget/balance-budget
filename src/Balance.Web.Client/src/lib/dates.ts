/*
 * Date formatters for lists and charts. The chart axis cares about cadence
 * (range dictates whether the day-of-month is informative); tooltips want a
 * full human-readable date. Everything goes through `i18n/format` so order,
 * month names, and ISO override follow the user's preference (ADR-0029);
 * parsing/arithmetic is `@internationalized/date` (ADR-0024) — no hand-rolled
 * ISO handling.
 *
 * Flavor rubric (docs/conventions.md → Date display): a day present ⇒ short
 * month; month+year headings ⇒ long month; weekday only in chart tooltips.
 */

import { getLocalTimeZone, today } from '@internationalized/date';
import type { TrendRange } from '../api/dashboard';
import { formatCalendarDate, parseIsoDate } from '../i18n/format';
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
        return formatCalendarDate(date, 'month-day');
    }
    if (range === '1Y') {
        return formatMonthAxisDate(date);
    }
    return formatCalendarDate(date, 'month');
}

/**
 * Shared month-bucketed x-axis tick: a short month name, with the year on
 * January to mark the boundary. One formatter so every monthly axis — the trend
 * chart, the register summary, and the Outlook projection — reads identically
 * (and stays region-aware via `formatCalendarDate`).
 */
export function formatMonthAxisDate(date: string): string {
    return parseIsoDate(date).getMonth() === 0
        ? formatCalendarDate(date, 'year-month')
        : formatCalendarDate(date, 'month');
}

/**
 * Bucket-aware x-axis tick for the Register summary chart: monthly buckets read
 * as month names (year on January to mark the boundary), daily and weekly
 * buckets keep the day-of-month.
 */
export function formatBucketAxisDate(date: string, bucket: RegisterSummaryBucketSize): string {
    if (bucket === 'Month') {
        return formatMonthAxisDate(date);
    }
    return formatCalendarDate(date, 'month-day');
}

/** Bucket-aware tooltip heading: "May 2026" for a month, a full date otherwise. */
export function formatBucketTooltipDate(date: string, bucket: RegisterSummaryBucketSize): string {
    if (bucket === 'Month') {
        return formatCalendarDate(date, 'year-month', { style: 'long' });
    }
    return formatCalendarDate(date, 'year-month-day', { weekday: true });
}

/** Full tooltip date with weekday, e.g. "Tue, 14 May 2026". */
export function formatTrendTooltipDate(date: string): string {
    return formatCalendarDate(date, 'year-month-day', { weekday: true });
}

/** A calendar date for list/table rows and detail headers, e.g. "12 Jun 2026"
 *  in region order. The single place a `YYYY-MM-DD` becomes a human date in a
 *  table, so every list reads the same and follows the preference (ADR-0029). */
export function formatTableDate(date: string): string {
    return formatCalendarDate(date, 'year-month-day');
}

/** A month-and-year label for schedule/amortization rows, e.g. "Jun 2026"
 *  (locale) or "2026-06" (ISO). Takes any `YYYY-MM…` period string. */
export function formatScheduleMonth(period: string): string {
    return formatCalendarDate(period.slice(0, 7), 'year-month');
}
