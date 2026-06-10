/*
 * Register summary (CONTEXT.md): the register bucketed into time periods,
 * segmented by direct child. The server takes an explicit bucket size; these
 * helpers derive it from the chart's date range and supply the default range
 * when the register's date filter is unset. Arithmetic is on the local
 * calendar via `@internationalized/date` (ADR-0024).
 */

import {
    type CalendarDate,
    getLocalTimeZone,
    parseDate,
    startOfMonth,
    today,
} from '@internationalized/date';

export type RegisterSummaryBucketSize = 'Day' | 'Week' | 'Month';

export type RegisterSummaryRange = { from: string; to: string };

/**
 * The chart's effective [from, to]: the register's date filter where set ('' means unset).
 * An unset `to` ends today; an unset `from` starts at the first of the month eleven months
 * back, so the default view is twelve whole monthly buckets.
 */
export function effectiveSummaryRange(
    filterFrom: string,
    filterTo: string,
    now: CalendarDate = today(getLocalTimeZone()),
): RegisterSummaryRange {
    const to = filterTo !== '' ? filterTo : now.toString();
    const from =
        filterFrom !== ''
            ? filterFrom
            : startOfMonth(parseDate(to).subtract({ months: 11 })).toString();
    return { from, to };
}

/**
 * Bucket size for a range: daily up to ~2 months, weekly up to ~6 months, monthly beyond —
 * so a short zoom shows day bars instead of one lonely bucket, and a long view stays readable.
 */
export function summaryBucketFor(range: RegisterSummaryRange): RegisterSummaryBucketSize {
    // CalendarDate.compare returns the difference in days.
    const days = parseDate(range.to).compare(parseDate(range.from)) + 1;
    if (days <= 62) return 'Day';
    if (days <= 183) return 'Week';
    return 'Month';
}
