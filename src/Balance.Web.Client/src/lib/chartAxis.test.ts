import { describe, expect, it } from 'vitest';
import { moneyAxis } from './chartAxis';

describe('moneyAxis', () => {
    it('returns undefined when there are no finite values', () => {
        expect(moneyAxis([])).toBeUndefined();
        expect(moneyAxis([NaN, Infinity])).toBeUndefined();
    });

    it('pads a positive line series and reports it as truncated', () => {
        const axis = moneyAxis([600_000, 1_200_000]);
        expect(axis).toBeDefined();
        if (!axis) return;
        // Lower bound sits below the data min (margin) but stays well above zero.
        expect(axis.domain[0]).toBeGreaterThan(0);
        expect(axis.domain[0]).toBeLessThan(600_000);
        expect(axis.domain[1]).toBeGreaterThan(1_200_000);
        expect(axis.truncated).toBe(true);
    });

    it('floors at zero and never truncates when includeZero is set', () => {
        const axis = moneyAxis([600_000, 1_200_000], { includeZero: true });
        expect(axis).toBeDefined();
        if (!axis) return;
        expect(axis.domain[0]).toBe(0);
        expect(axis.truncated).toBe(false);
        // Headroom is added above the peak.
        expect(axis.domain[1]).toBeGreaterThan(1_200_000);
    });

    it('pads both sides of a signed range and keeps zero in view', () => {
        const axis = moneyAxis([-400_000, 900_000], { includeZero: true });
        expect(axis).toBeDefined();
        if (!axis) return;
        expect(axis.domain[0]).toBeLessThan(-400_000);
        expect(axis.domain[1]).toBeGreaterThan(900_000);
        expect(axis.truncated).toBe(false);
    });

    it('emits clean, rounded ticks within the domain', () => {
        const axis = moneyAxis([600_000, 1_200_000]);
        expect(axis).toBeDefined();
        if (!axis) return;
        expect(axis.ticks.length).toBeGreaterThan(1);
        for (const tick of axis.ticks) {
            expect(tick).toBeGreaterThanOrEqual(axis.domain[0]);
            expect(tick).toBeLessThanOrEqual(axis.domain[1]);
            // Steps land on a 1/2/5 × 10ⁿ grid, so ticks are whole hundred-thousands here.
            expect(tick % 100_000).toBe(0);
        }
    });

    it('does not pad a non-negative series below zero even without includeZero', () => {
        // A series sitting near zero shouldn't read as truncated.
        const axis = moneyAxis([0, 50_000]);
        expect(axis).toBeDefined();
        if (!axis) return;
        expect(axis.truncated).toBe(false);
        expect(axis.domain[0]).toBeLessThanOrEqual(0);
    });
});
