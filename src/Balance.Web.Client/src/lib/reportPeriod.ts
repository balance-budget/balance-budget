/*
 * Reporting period (CONTEXT.md): the inclusive [from, to] window of calendar
 * dates that scopes an Insights Report. Presets are pure conveniences that set
 * from/to; the canonical state is the two ISO dates, which live in the route's
 * search params so a period is shareable. All arithmetic is on the local
 * calendar (the wire dates are DateOnly, so there's no time-of-day to mind).
 */

export type ReportPeriod = { from: string; to: string };

export type PeriodPreset =
    | 'this-month'
    | 'last-month'
    | 'this-year'
    | 'last-year'
    | 'last-30'
    | 'last-90'
    | 'custom';

export const PERIOD_PRESETS: { token: Exclude<PeriodPreset, 'custom'>; label: string }[] = [
    { token: 'this-month', label: 'This month' },
    { token: 'last-month', label: 'Last month' },
    { token: 'this-year', label: 'This year' },
    { token: 'last-year', label: 'Last year' },
    { token: 'last-30', label: 'Last 30 days' },
    { token: 'last-90', label: 'Last 90 days' },
];

function iso(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
}

const startOfMonth = (d: Date) => new Date(d.getFullYear(), d.getMonth(), 1);
const endOfMonth = (d: Date) => new Date(d.getFullYear(), d.getMonth() + 1, 0);
const startOfYear = (d: Date) => new Date(d.getFullYear(), 0, 1);
const endOfYear = (d: Date) => new Date(d.getFullYear(), 11, 31);
const addDays = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);

/** The [from, to] range for a preset, evaluated against `today` (defaults to now). */
export function presetRange(
    preset: Exclude<PeriodPreset, 'custom'>,
    today: Date = new Date(),
): ReportPeriod {
    switch (preset) {
        case 'this-month':
            return { from: iso(startOfMonth(today)), to: iso(endOfMonth(today)) };
        case 'last-month': {
            const m = new Date(today.getFullYear(), today.getMonth() - 1, 1);
            return { from: iso(startOfMonth(m)), to: iso(endOfMonth(m)) };
        }
        case 'this-year':
            return { from: iso(startOfYear(today)), to: iso(endOfYear(today)) };
        case 'last-year': {
            const y = new Date(today.getFullYear() - 1, 0, 1);
            return { from: iso(startOfYear(y)), to: iso(endOfYear(y)) };
        }
        case 'last-30':
            return { from: iso(addDays(today, -29)), to: iso(today) };
        case 'last-90':
            return { from: iso(addDays(today, -89)), to: iso(today) };
    }
}

/** The default period when a route carries no params: the current month. */
export function defaultPeriod(today: Date = new Date()): ReportPeriod {
    return presetRange('this-month', today);
}

/** Which preset a period matches exactly, or 'custom' when it lines up with none. */
export function detectPreset(period: ReportPeriod, today: Date = new Date()): PeriodPreset {
    for (const { token } of PERIOD_PRESETS) {
        const range = presetRange(token, today);
        if (range.from === period.from && range.to === period.to) return token;
    }
    return 'custom';
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Coerce a raw search value into an ISO date, or null when it isn't one. */
export function parseIsoDate(raw: unknown): string | null {
    return typeof raw === 'string' && ISO_DATE.test(raw) ? raw : null;
}
