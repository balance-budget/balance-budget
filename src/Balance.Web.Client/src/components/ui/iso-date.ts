import {
    type CalendarDate,
    type CalendarDateTime,
    getLocalTimeZone,
    parseAbsoluteToLocal,
    parseDate,
    toCalendarDateTime,
    toZoned,
} from '@internationalized/date';

/**
 * `''` ⇢ null; otherwise a strict ISO `yyyy-MM-dd` parse. The app keeps ISO
 * strings canonical (the wire is `DateOnly`); `CalendarDate` exists only
 * inside the ui/ date components (ADR-0024).
 */
export function parseIsoDate(value: string): CalendarDate | null {
    if (value === '') return null;
    try {
        return parseDate(value);
    } catch {
        return null;
    }
}

/**
 * A UTC instant (ISO 8601, e.g. `2026-06-24T12:30:00.000Z`) ⇢ the local
 * wall-clock `CalendarDateTime` shown in the segmented field; `''` ⇢ null.
 * The timezone stays inside the ui/ date components (ADR-0024) — screens only
 * ever see the canonical instant string.
 */
export function parseIsoInstant(value: string): CalendarDateTime | null {
    if (value === '') return null;
    try {
        return toCalendarDateTime(parseAbsoluteToLocal(value));
    } catch {
        return null;
    }
}

/** The inverse of {@link parseIsoInstant}: local wall-clock ⇢ UTC instant ISO. */
export function isoInstantFromLocal(date: CalendarDateTime): string {
    return toZoned(date, getLocalTimeZone()).toAbsoluteString();
}
