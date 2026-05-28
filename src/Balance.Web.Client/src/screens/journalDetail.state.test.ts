import { describe, expect, it } from 'vitest';
import { asAccountId, asJournalLineId } from '../lib/domain';
import {
    buildReplaceRequest,
    computeTotals,
    emptyLine,
    isLineLocked,
    toEditLines,
    type EditLine,
    type LoadedLine,
} from './journalDetail.state';

const groceries = asAccountId('11111111-1111-1111-1111-111111111111');
const checking = asAccountId('22222222-2222-2222-2222-222222222222');
const savings = asAccountId('33333333-3333-3333-3333-333333333333');

const lineA = asJournalLineId('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
const lineB = asJournalLineId('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');

function unclearedLoaded(): LoadedLine[] {
    return [
        {
            id: lineA,
            accountId: groceries,
            amount: 4000,
            description: 'AH',
            reconciliationStatus: 'Uncleared',
        },
        {
            id: lineB,
            accountId: checking,
            amount: -4000,
            description: null,
            reconciliationStatus: 'Uncleared',
        },
    ];
}

function clearedBankSideLoaded(): LoadedLine[] {
    return [
        {
            id: lineA,
            accountId: groceries,
            amount: 4000,
            description: null,
            reconciliationStatus: 'Uncleared',
        },
        {
            id: lineB,
            accountId: checking,
            amount: -4000,
            description: null,
            reconciliationStatus: 'Cleared',
        },
    ];
}

describe('toEditLines', () => {
    it('projects existing lines, preserving id and status, splitting amount into side+magnitude', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        expect(lines).toHaveLength(2);
        expect(lines[0]).toMatchObject({
            serverId: lineA,
            status: 'Uncleared',
            side: 'debit',
            amount: '40.00',
            description: 'AH',
        });
        expect(lines[1]).toMatchObject({
            serverId: lineB,
            status: 'Uncleared',
            side: 'credit',
            amount: '40.00',
        });
    });

    it('uses the JournalLineId as the React key so frozen rows stay stable', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        expect(lines[0]?.key).toBe(lineA);
        expect(lines[1]?.key).toBe(lineB);
    });
});

describe('isLineLocked', () => {
    it('treats Cleared and Reconciled as locked', () => {
        const cleared: EditLine = { ...emptyLine(), status: 'Cleared' };
        const reconciled: EditLine = { ...emptyLine(), status: 'Reconciled' };
        expect(isLineLocked(cleared)).toBe(true);
        expect(isLineLocked(reconciled)).toBe(true);
    });

    it('leaves Uncleared lines unlocked', () => {
        expect(isLineLocked(emptyLine())).toBe(false);
    });
});

describe('computeTotals', () => {
    it('sums debit and credit magnitudes and flags balanced when equal', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const totals = computeTotals(lines, 2);
        expect(totals.debitMinor).toBe(4000);
        expect(totals.creditMinor).toBe(4000);
        expect(totals.balanced).toBe(true);
    });

    it('flags imbalanced when user edits one side magnitude', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const edited: EditLine[] = lines.map((l, i) => (i === 0 ? { ...l, amount: '50.00' } : l));
        const totals = computeTotals(edited, 2);
        expect(totals.balanced).toBe(false);
        expect(totals.debitMinor).toBe(5000);
        expect(totals.creditMinor).toBe(4000);
    });

    it('skips lines that fail to parse', () => {
        const lines: EditLine[] = [{ ...emptyLine(), accountId: groceries, amount: 'notanumber' }];
        const totals = computeTotals(lines, 2);
        expect(totals.debitMinor).toBe(0);
        expect(totals.creditMinor).toBe(0);
    });
});

describe('buildReplaceRequest', () => {
    it('emits a balanced full-body PUT echoing existing line ids', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: 'AH groceries',
            counterpartyId: null,
            lines,
            scale: 2,
        });

        expect(result.ok).toBe(true);
        if (!result.ok) return;
        expect(result.request.date).toBe('2026-05-27');
        expect(result.request.description).toBe('AH groceries');
        expect(result.request.lines).toHaveLength(2);
        expect(result.request.lines[0]).toMatchObject({
            id: lineA,
            accountId: groceries,
            amount: 4000,
            reconciliationStatus: 'Uncleared',
        });
        expect(result.request.lines[1]).toMatchObject({
            id: lineB,
            accountId: checking,
            amount: -4000,
        });
    });

    it('round-trips a Cleared bank-side line with its status and original AccountId/Amount', () => {
        const lines = toEditLines(clearedBankSideLoaded(), 2);
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: '',
            counterpartyId: null,
            lines,
            scale: 2,
        });

        expect(result.ok).toBe(true);
        if (!result.ok) return;
        const cleared = result.request.lines.find(l => l.id === lineB);
        expect(cleared).toBeDefined();
        expect(cleared?.reconciliationStatus).toBe('Cleared');
        expect(cleared?.accountId).toBe(checking);
        expect(cleared?.amount).toBe(-4000);
    });

    it('emits null serverId on newly added lines', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const newLine: EditLine = {
            ...emptyLine(),
            accountId: savings,
            side: 'debit',
            amount: '10.00',
        };
        const edited: EditLine[] = lines.map((l, i) => (i === 0 ? { ...l, amount: '30.00' } : l));
        const withNew = [...edited, newLine];
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: '',
            counterpartyId: null,
            lines: withNew,
            scale: 2,
        });

        expect(result.ok).toBe(true);
        if (!result.ok) return;
        const inserted = result.request.lines.find(l => l.accountId === savings);
        expect(inserted).toBeDefined();
        expect(inserted?.id).toBeNull();
        expect(inserted?.reconciliationStatus).toBeNull();
        expect(inserted?.amount).toBe(1000);
        const totalSigned = result.request.lines.reduce((s, l) => s + Number(l.amount), 0);
        expect(totalSigned).toBe(0);
    });

    it('rejects an unbalanced edit with a top-level error', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const edited: EditLine[] = lines.map((l, i) => (i === 0 ? { ...l, amount: '50.00' } : l));
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: '',
            counterpartyId: null,
            lines: edited,
            scale: 2,
        });

        expect(result.ok).toBe(false);
        if (result.ok) return;
        expect(result.topError).toBeDefined();
    });

    it('rejects when a line is missing an account', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const edited: EditLine[] = lines.map((l, i) => (i === 0 ? { ...l, accountId: null } : l));
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: '',
            counterpartyId: null,
            lines: edited,
            scale: 2,
        });

        expect(result.ok).toBe(false);
        if (result.ok) return;
        expect(result.fieldErrors['lines[0].accountId']).toBeDefined();
    });

    it('rejects when fewer than two non-empty lines remain', () => {
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: '',
            counterpartyId: null,
            lines: [
                {
                    ...emptyLine(),
                    accountId: groceries,
                    side: 'debit',
                    amount: '10.00',
                },
            ],
            scale: 2,
        });

        expect(result.ok).toBe(false);
        if (result.ok) return;
        expect(result.fieldErrors.lines).toBeDefined();
    });

    it('echoes counterpartyId untouched when supplied', () => {
        const lines = toEditLines(unclearedLoaded(), 2);
        const result = buildReplaceRequest({
            date: '2026-05-27',
            description: '',
            counterpartyId: null,
            lines,
            scale: 2,
        });
        expect(result.ok).toBe(true);
        if (!result.ok) return;
        expect(result.request.counterpartyId).toBeNull();
    });
});
