import { describe, expect, it } from 'vitest';
import {
    buildOptionList,
    matchesQuery,
    nextActiveIndex,
    type ComboboxItem,
} from './combobox.state';

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
});

describe('buildOptionList', () => {
    it('returns every item when the query is empty', () => {
        const out = buildOptionList({ items: items(), query: '' });
        expect(out).toHaveLength(3);
        expect(out.every(o => o.kind === 'item')).toBe(true);
    });

    it('filters by substring (case-insensitive)', () => {
        const out = buildOptionList({ items: items(), query: 'be' });
        expect(out).toHaveLength(2);
        expect(out.map(o => (o.kind === 'item' ? o.item.label : o.kind))).toEqual([
            'Albert Heijn',
            'Beta Bank',
        ]);
    });

    it('prepends a "None" sentinel when noneLabel is supplied', () => {
        const out = buildOptionList({
            items: items(),
            query: '',
            noneLabel: '── None (self-transfer)',
        });
        expect(out[0]).toEqual({ kind: 'none', label: '── None (self-transfer)' });
        expect(out.slice(1).every(o => o.kind === 'item')).toBe(true);
    });

    it('appends a "+ Create" sentinel when query is non-empty and no exact match exists', () => {
        const out = buildOptionList({
            items: items(),
            query: 'Xeon',
            createLabel: typed => `+ Create '${typed}'`,
        });
        expect(out[out.length - 1]).toEqual({
            kind: 'create',
            label: "+ Create 'Xeon'",
            typed: 'Xeon',
        });
    });

    it('omits the "+ Create" sentinel on an exact (case-insensitive) match', () => {
        const out = buildOptionList({
            items: items(),
            query: 'albert heijn',
            createLabel: typed => `+ Create '${typed}'`,
        });
        expect(out.some(o => o.kind === 'create')).toBe(false);
    });

    it('matches against searchText when present, not the displayed label', () => {
        const withSearch: ComboboxItem<string>[] = [
            { key: 'a', label: 'Car › Tax', searchText: '5110 Car Tax', value: 'a' },
            { key: 'b', label: 'Home › Tax', searchText: '5210 Home Tax', value: 'b' },
        ];
        // The code lives only in searchText, yet the query finds it.
        expect(buildOptionList({ items: withSearch, query: '5110' })).toHaveLength(1);
        // A path segment that the space-joined searchText exposes but the label hides.
        expect(buildOptionList({ items: withSearch, query: 'car t' })).toHaveLength(1);
        // The leaf still matches both rows.
        expect(buildOptionList({ items: withSearch, query: 'tax' })).toHaveLength(2);
    });

    it('orders groups by the supplied groupOrder', () => {
        const out = buildOptionList({
            items: items(),
            query: '',
            groupOrder: ['Asset', 'Expense'],
        });
        const groups = out.flatMap(o => (o.kind === 'item' ? [o.group ?? ''] : []));
        expect(groups).toEqual(['Asset', 'Expense', 'Expense']);
    });
});

describe('nextActiveIndex', () => {
    it('wraps off the end with ArrowDown', () => {
        expect(nextActiveIndex(2, 3, 1)).toBe(0);
    });

    it('wraps off the start with ArrowUp', () => {
        expect(nextActiveIndex(0, 3, -1)).toBe(2);
    });

    it('returns 0 for ArrowDown from no-active', () => {
        expect(nextActiveIndex(-1, 3, 1)).toBe(0);
    });

    it('returns the last index for ArrowUp from no-active', () => {
        expect(nextActiveIndex(-1, 3, -1)).toBe(2);
    });

    it('returns -1 for an empty list', () => {
        expect(nextActiveIndex(0, 0, 1)).toBe(-1);
    });
});
