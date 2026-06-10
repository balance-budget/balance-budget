import { describe, expect, it } from 'vitest';
import { CalendarDate } from '@internationalized/date';
import { effectiveSummaryRange, summaryBucketFor } from './registerSummary';

// A fixed "today" mid-month so month-boundary math is unambiguous.
const TODAY = new CalendarDate(2026, 6, 10);

describe('effectiveSummaryRange', () => {
    it('passes a fully-set filter range through untouched', () => {
        expect(effectiveSummaryRange('2026-01-01', '2026-03-31', TODAY)).toEqual({
            from: '2026-01-01',
            to: '2026-03-31',
        });
    });

    it('defaults to twelve whole monthly buckets ending today', () => {
        expect(effectiveSummaryRange('', '', TODAY)).toEqual({
            from: '2025-07-01',
            to: '2026-06-10',
        });
    });

    it('anchors a missing from to the set to', () => {
        expect(effectiveSummaryRange('', '2025-12-31', TODAY)).toEqual({
            from: '2025-01-01',
            to: '2025-12-31',
        });
    });

    it('ends a missing to at today', () => {
        expect(effectiveSummaryRange('2026-05-01', '', TODAY)).toEqual({
            from: '2026-05-01',
            to: '2026-06-10',
        });
    });
});

describe('summaryBucketFor', () => {
    it('uses daily buckets up to two months', () => {
        expect(summaryBucketFor({ from: '2026-06-01', to: '2026-06-30' })).toBe('Day');
        expect(summaryBucketFor({ from: '2026-05-01', to: '2026-07-01' })).toBe('Day');
    });

    it('uses weekly buckets up to six months', () => {
        expect(summaryBucketFor({ from: '2026-04-01', to: '2026-06-30' })).toBe('Week');
        expect(summaryBucketFor({ from: '2026-01-01', to: '2026-06-30' })).toBe('Week');
    });

    it('uses monthly buckets beyond six months', () => {
        expect(summaryBucketFor({ from: '2026-01-01', to: '2026-12-31' })).toBe('Month');
        expect(summaryBucketFor({ from: '2025-07-01', to: '2026-06-10' })).toBe('Month');
    });
});
