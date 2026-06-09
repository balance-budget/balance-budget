/*
 * Reporting period (CONTEXT.md): the inclusive [from, to] window of calendar
 * dates that scopes an Insights Report. Presets are pure conveniences that set
 * from/to; the canonical state is the two ISO dates, which live in the route's
 * search params so a period is shareable. All arithmetic is on the local
 * calendar via `@internationalized/date` (the wire dates are DateOnly, so
 * there's no time-of-day to mind).
 */

import { msg } from '@lingui/core/macro';
import type { MessageDescriptor } from '@lingui/core';
import {
    type CalendarDate,
    endOfMonth,
    endOfYear,
    getLocalTimeZone,
    startOfMonth,
    startOfYear,
    today,
} from '@internationalized/date';

export type ReportPeriod = { from: string; to: string };

export type PeriodPreset =
    | 'this-month'
    | 'last-month'
    | 'this-year'
    | 'last-year'
    | 'last-30'
    | 'last-90'
    | 'custom';

// Labels are MessageDescriptors (the `msg` macro) so they're extracted for
// translation; resolve them at the render site with `i18n._(label)`.
export const PERIOD_PRESETS: {
    token: Exclude<PeriodPreset, 'custom'>;
    label: MessageDescriptor;
}[] = [
    { token: 'this-month', label: msg`This month` },
    { token: 'last-month', label: msg`Last month` },
    { token: 'this-year', label: msg`This year` },
    { token: 'last-year', label: msg`Last year` },
    { token: 'last-30', label: msg`Last 30 days` },
    { token: 'last-90', label: msg`Last 90 days` },
];

function range(from: CalendarDate, to: CalendarDate): ReportPeriod {
    return { from: from.toString(), to: to.toString() };
}

/** The [from, to] range for a preset, evaluated against `now` (defaults to today). */
export function presetRange(
    preset: Exclude<PeriodPreset, 'custom'>,
    now: CalendarDate = today(getLocalTimeZone()),
): ReportPeriod {
    switch (preset) {
        case 'this-month':
            return range(startOfMonth(now), endOfMonth(now));
        case 'last-month': {
            const m = now.subtract({ months: 1 });
            return range(startOfMonth(m), endOfMonth(m));
        }
        case 'this-year':
            return range(startOfYear(now), endOfYear(now));
        case 'last-year': {
            const y = now.subtract({ years: 1 });
            return range(startOfYear(y), endOfYear(y));
        }
        case 'last-30':
            return range(now.subtract({ days: 29 }), now);
        case 'last-90':
            return range(now.subtract({ days: 89 }), now);
    }
}

/** The default period when a route carries no params: the current month. */
export function defaultPeriod(now: CalendarDate = today(getLocalTimeZone())): ReportPeriod {
    return presetRange('this-month', now);
}

/** Which preset a period matches exactly, or 'custom' when it lines up with none. */
export function detectPreset(
    period: ReportPeriod,
    now: CalendarDate = today(getLocalTimeZone()),
): PeriodPreset {
    for (const { token } of PERIOD_PRESETS) {
        const preset = presetRange(token, now);
        if (preset.from === period.from && preset.to === period.to) return token;
    }
    return 'custom';
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Coerce a raw search value into an ISO date, or null when it isn't one. */
export function parseIsoDate(raw: unknown): string | null {
    return typeof raw === 'string' && ISO_DATE.test(raw) ? raw : null;
}
