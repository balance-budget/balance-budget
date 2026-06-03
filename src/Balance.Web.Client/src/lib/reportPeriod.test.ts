import { describe, expect, it } from 'vitest';
import { defaultPeriod, detectPreset, parseIsoDate, presetRange } from './reportPeriod';

// A fixed "today" mid-month so month/year boundary math is unambiguous.
const TODAY = new Date(2026, 4, 15); // 2026-05-15 (local)

describe('presetRange', () => {
    it('this-month spans the first to the last day of the current month', () => {
        expect(presetRange('this-month', TODAY)).toEqual({ from: '2026-05-01', to: '2026-05-31' });
    });

    it('last-month clamps to the prior calendar month', () => {
        expect(presetRange('last-month', TODAY)).toEqual({ from: '2026-04-01', to: '2026-04-30' });
    });

    it('this-year spans the whole calendar year', () => {
        expect(presetRange('this-year', TODAY)).toEqual({ from: '2026-01-01', to: '2026-12-31' });
    });

    it('last-year spans the previous calendar year', () => {
        expect(presetRange('last-year', TODAY)).toEqual({ from: '2025-01-01', to: '2025-12-31' });
    });

    it('last-30 is an inclusive 30-day window ending today', () => {
        expect(presetRange('last-30', TODAY)).toEqual({ from: '2026-04-16', to: '2026-05-15' });
    });

    it('last-90 is an inclusive 90-day window ending today', () => {
        expect(presetRange('last-90', TODAY)).toEqual({ from: '2026-02-15', to: '2026-05-15' });
    });
});

describe('defaultPeriod', () => {
    it('is the current month', () => {
        expect(defaultPeriod(TODAY)).toEqual(presetRange('this-month', TODAY));
    });
});

describe('detectPreset', () => {
    it('recognises a range that matches a preset exactly', () => {
        expect(detectPreset({ from: '2026-05-01', to: '2026-05-31' }, TODAY)).toBe('this-month');
        expect(detectPreset({ from: '2025-01-01', to: '2025-12-31' }, TODAY)).toBe('last-year');
    });

    it('falls back to custom for an arbitrary range', () => {
        expect(detectPreset({ from: '2026-05-03', to: '2026-05-19' }, TODAY)).toBe('custom');
    });
});

describe('parseIsoDate', () => {
    it('accepts a YYYY-MM-DD string', () => {
        expect(parseIsoDate('2026-05-15')).toBe('2026-05-15');
    });

    it('rejects anything else', () => {
        expect(parseIsoDate('15-05-2026')).toBeNull();
        expect(parseIsoDate(42)).toBeNull();
        expect(parseIsoDate(undefined)).toBeNull();
    });
});
