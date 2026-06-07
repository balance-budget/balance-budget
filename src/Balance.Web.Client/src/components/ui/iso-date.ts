import { type CalendarDate, parseDate } from '@internationalized/date';

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
