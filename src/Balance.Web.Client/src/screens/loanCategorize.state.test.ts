import { describe, expect, it } from 'vitest';
import type { LoanProposal } from '../api/loans';
import { asAccountId, asLoanId, asLoanPartId } from '../lib/domain';
import {
    buildRequest,
    initialForm,
    linesFromLoanProposal,
} from './bankTransactionCategorize.state';

const partA = asLoanPartId('00000000-0000-7000-8000-00000000000a');
const partB = asLoanPartId('00000000-0000-7000-8000-00000000000b');
const partAccountA = asAccountId('00000000-0000-7000-8000-0000000000a1');
const partAccountB = asAccountId('00000000-0000-7000-8000-0000000000b1');
const interestAccount = asAccountId('00000000-0000-7000-8000-0000000000ee');

const formatTwoDecimals = (minor: number): string => {
    const major = Math.floor(Math.abs(minor) / 100);
    const remainder = Math.abs(minor) - major * 100;
    return `${major.toString()}.${remainder.toString().padStart(2, '0')}`;
};

function proposal(): LoanProposal {
    return {
        loanId: asLoanId('00000000-0000-7000-8000-000000000001'),
        month: '2026-06-01',
        currencyCode: 'EUR',
        interestExpenseAccountId: interestAccount,
        lines: [
            {
                loanPartId: partA,
                label: 'Part 1',
                partAccountId: partAccountA,
                interest: 30_000,
                principal: 60_000,
                payment: 90_000,
            },
            {
                loanPartId: partB,
                label: 'Part 2',
                partAccountId: partAccountB,
                interest: 20_000,
                principal: 0, // interest-only
                payment: 20_000,
            },
        ],
        total: 110_000,
        depositOffset: null,
    };
}

describe('linesFromLoanProposal', () => {
    it('emits a principal line per amortizing part and an interest line per part', () => {
        const lines = linesFromLoanProposal(proposal(), null, formatTwoDecimals);

        expect(lines).toHaveLength(3); // Part 2 is interest-only: no principal line
        expect(lines[0]).toMatchObject({
            accountId: partAccountA,
            amount: '600.00',
            loanPartId: partA,
        });
        expect(lines[1]).toMatchObject({
            accountId: interestAccount,
            amount: '300.00',
            loanPartId: partA,
        });
        expect(lines[2]).toMatchObject({
            accountId: interestAccount,
            amount: '200.00',
            loanPartId: partB,
        });
    });

    it('scopes to a subset of parts', () => {
        const lines = linesFromLoanProposal(proposal(), new Set([partB]), formatTwoDecimals);

        expect(lines).toHaveLength(1);
        expect(lines[0]).toMatchObject({ accountId: interestAccount, loanPartId: partB });
    });

    it('falls back to one empty line when nothing is included', () => {
        const lines = linesFromLoanProposal(proposal(), new Set(), formatTwoDecimals);

        expect(lines).toHaveLength(1);
        expect(lines[0]?.accountId).toBeNull();
    });

    it('appends a credit offset line when the proposal carries a deposit offset', () => {
        const depositIncome = asAccountId('00000000-0000-7000-8000-0000000000df');
        const withOffset: LoanProposal = {
            ...proposal(),
            depositOffset: { incomeAccountId: depositIncome, amount: 12_000 },
        };

        const lines = linesFromLoanProposal(withOffset, null, formatTwoDecimals);

        expect(lines).toHaveLength(4);
        expect(lines[3]).toMatchObject({
            accountId: depositIncome,
            amount: '120.00',
            credit: true,
        });
    });
});

describe('buildRequest with a deposit offset', () => {
    it('nets the credit offset so lines balance to the bank debit', () => {
        const depositIncome = asAccountId('00000000-0000-7000-8000-0000000000df');
        const withOffset: LoanProposal = {
            ...proposal(),
            depositOffset: { incomeAccountId: depositIncome, amount: 12_000 },
        };
        // Gross 110_000 less the 12_000 offset = 98_000 net debit on the statement.
        const form = initialForm({
            today: '2026-06-07',
            bookingDate: '2026-06-01',
            description: 'Monthly payment',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: -98_000,
            formatMagnitude: formatTwoDecimals,
        });
        form.lines = linesFromLoanProposal(withOffset, null, formatTwoDecimals);

        const result = buildRequest(form, -98_000, 2);

        expect(result.ok).toBe(true);
        if (!result.ok) return;
        // Counter-side debits are positive; the offset is a negative (credit) income line.
        expect(result.request.lines.map(l => l.amount)).toEqual([60_000, 30_000, 20_000, -12_000]);
        expect(result.request.lines[3]?.accountId).toBe(depositIncome);
    });
});

describe('buildRequest with loan attributions', () => {
    it('carries each line attribution onto the wire', () => {
        const form = initialForm({
            today: '2026-06-07',
            bookingDate: '2026-06-01',
            description: 'Monthly payment',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: -110_000,
            formatMagnitude: formatTwoDecimals,
        });
        form.lines = linesFromLoanProposal(proposal(), null, formatTwoDecimals);

        const result = buildRequest(form, -110_000, 2);

        expect(result.ok).toBe(true);
        if (!result.ok) return;
        expect(result.request.lines).toHaveLength(3);
        expect(result.request.lines.map(l => l.loanPartId)).toEqual([partA, partA, partB]);
        // Counter-side of a debit is positive; amounts editable but pre-filled exactly.
        expect(result.request.lines.map(l => l.amount)).toEqual([60_000, 30_000, 20_000]);
    });
});
