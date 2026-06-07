import { describe, expect, it } from 'vitest';
import { groupBuckets, matchesQuery, type ComboboxItem } from './combobox.state';

function items(): ComboboxItem<string>[] {
    return [
        { key: 'a', label: 'Albert Heijn', group: 'Expense', value: 'a' },
        { key: 'b', label: 'Beta Bank', group: 'Asset', value: 'b' },
        { key: 'c', label: 'Coffee Shop', group: 'Expense', value: 'c' },
    ];
}

describe('matchesQuery', () => {
    it('matches case-insensitively', () => {
        expect(matchesQuery('Albert Heijn', 'alb')).toBe(true);
        expect(matchesQuery('Albert Heijn', 'HEI')).toBe(true);
    });
    it('matches all labels for an empty query', () => {
        expect(matchesQuery('anything', '   ')).toBe(true);
        expect(matchesQuery('anything', '')).toBe(true);
    });
    it('returns false on no substring overlap', () => {
        expect(matchesQuery('Coffee Shop', 'beta')).toBe(false);
    });
    it('matches against searchText facets the label hides', () => {
        // The account picker matches "5110" and "car t" via the space-joined
        // searchText ("5110 Car Tax") even though the label reads "Car › Tax".
        expect(matchesQuery('5110 Car Tax', '5110')).toBe(true);
        expect(matchesQuery('5110 Car Tax', 'car t')).toBe(true);
        expect(matchesQuery('5210 Home Tax', 'car t')).toBe(false);
    });
});

describe('groupBuckets', () => {
    it('orders groups by the supplied groupOrder', () => {
        const out = groupBuckets(items(), ['Asset', 'Expense']);
        expect(out.map(b => b.group)).toEqual(['Asset', 'Expense']);
        expect(out[1]?.items.map(i => i.label)).toEqual(['Albert Heijn', 'Coffee Shop']);
    });

    it('keeps first-seen order without a groupOrder', () => {
        const out = groupBuckets(items(), undefined);
        expect(out.map(b => b.group)).toEqual(['Expense', 'Asset']);
    });

    it('appends groups missing from groupOrder in first-seen order', () => {
        const out = groupBuckets(items(), ['Asset']);
        expect(out.map(b => b.group)).toEqual(['Asset', 'Expense']);
    });

    it('buckets ungrouped items under undefined', () => {
        const out = groupBuckets([{ key: 'x', label: 'X', value: 'x' }], ['Asset']);
        expect(out).toEqual([{ group: undefined, items: [{ key: 'x', label: 'X', value: 'x' }] }]);
    });
});
