import { describe, expect, it } from 'vitest';
import type { RegisterRow } from '../api/register';
import type { ReconciliationStatus } from '../api/register';
import { asAccountId, asCounterpartyId, asJournalEntryId, asJournalLineId } from '../lib/domain';
import { disabledLineKeys, prunePageSelection, selectableLineIds } from './accountRegister.state';

function row(lineId: string, status: ReconciliationStatus): RegisterRow {
    return {
        journalEntryId: asJournalEntryId(`e-${lineId}`),
        journalLineId: asJournalLineId(lineId),
        accountId: asAccountId('a'),
        accountName: 'Checking',
        date: '2026-01-01',
        entryDescription: null,
        counterpartyId: asCounterpartyId('c'),
        counterpartyName: 'Acme',
        lineDescription: null,
        reconciliationStatus: status,
        amount: { amount: 100, currencyCode: 'EUR' },
        counter: [],
    };
}

const rows: RegisterRow[] = [
    row('1', 'Uncleared'),
    row('2', 'Cleared'),
    row('3', 'Uncleared'),
    row('4', 'Reconciled'),
];

describe('selectableLineIds', () => {
    it('returns only Uncleared line ids', () => {
        expect(selectableLineIds(rows).map(String)).toEqual(['1', '3']);
    });
});

describe('disabledLineKeys', () => {
    it('returns every non-Uncleared line id', () => {
        expect([...disabledLineKeys(rows)].map(String).sort()).toEqual(['2', '4']);
    });
});

describe('prunePageSelection', () => {
    it('keeps only ids that are still on the page and movable', () => {
        const selected = new Set([
            asJournalLineId('1'),
            asJournalLineId('2'), // cleared — drops
            asJournalLineId('99'), // off page — drops
        ]);
        expect([...prunePageSelection(rows, selected)].map(String)).toEqual(['1']);
    });

    it('returns an empty set when nothing matches', () => {
        expect(prunePageSelection(rows, new Set()).size).toBe(0);
    });
});
