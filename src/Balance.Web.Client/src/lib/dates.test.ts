import { describe, expect, it } from 'vitest';
import { isValidIsoDate } from './dates';

describe('isValidIsoDate', () => {
    it('accepts real calendar days in yyyy-MM-dd form', () => {
        expect(isValidIsoDate('2026-06-06')).toBe(true);
        expect(isValidIsoDate('2024-02-29')).toBe(true); // leap day
        expect(isValidIsoDate('2026-12-31')).toBe(true);
    });

    it('rejects malformed strings', () => {
        expect(isValidIsoDate('')).toBe(false);
        expect(isValidIsoDate('2026-6-6')).toBe(false); // unpadded
        expect(isValidIsoDate('06/06/2026')).toBe(false); // US format
        expect(isValidIsoDate('2026-06-06T00:00')).toBe(false);
    });

    it('rejects impossible dates', () => {
        expect(isValidIsoDate('2026-13-01')).toBe(false); // month 13
        expect(isValidIsoDate('2026-02-30')).toBe(false); // Feb 30 rolls over
        expect(isValidIsoDate('2025-02-29')).toBe(false); // not a leap year
    });
});
