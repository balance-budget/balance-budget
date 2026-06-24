import { describe, expect, it } from 'vitest';
import { asAccountId } from '../lib/domain';
import {
    buildDeposit,
    buildPartRequest,
    type DepositDraft,
    type PartDraft,
} from './loanForm.state';

function part(overrides: Partial<PartDraft> = {}): PartDraft {
    return {
        id: 'part-1',
        label: 'Tranche A',
        repaymentType: 'Annuity',
        startDate: '2026-01-01',
        endDate: '2056-01-01',
        mode: 'adopt',
        adoptAccountId: asAccountId('11111111-1111-1111-1111-111111111111'),
        newName: '',
        newCode: '',
        openingBalance: '',
        ratePercent: '3.8',
        fixedUntil: '',
        ...overrides,
    };
}

describe('buildPartRequest', () => {
    it('rejects a blank label, a missing end date, and a non-positive rate', () => {
        expect(buildPartRequest(part({ label: '  ' }), 2).ok).toBe(false);
        expect(buildPartRequest(part({ endDate: '' }), 2).ok).toBe(false);
        expect(buildPartRequest(part({ ratePercent: '' }), 2).ok).toBe(false);
        expect(buildPartRequest(part({ ratePercent: '-1' }), 2).ok).toBe(false);
    });

    it('adopts an existing account and carries the opening rate period', () => {
        const result = buildPartRequest(part(), 2);
        expect(result.ok).toBe(true);
        if (!result.ok) return;
        expect(result.request.adoptAccountId).toBe(part().adoptAccountId);
        expect(result.request.newAccount).toBeNull();
        expect(result.request.ratePeriods).toEqual([
            { effectiveDate: '2026-01-01', annualRatePercent: 3.8, fixedUntil: null },
        ]);
    });

    it('requires an account when mode is adopt', () => {
        expect(buildPartRequest(part({ adoptAccountId: null }), 2).ok).toBe(false);
    });

    it('builds a fresh account with the opening balance in minor units', () => {
        const result = buildPartRequest(
            part({
                mode: 'new',
                adoptAccountId: null,
                newName: 'Mortgage A',
                newCode: '2400',
                openingBalance: '250000',
                fixedUntil: '2036-01-01',
            }),
            2,
        );
        expect(result.ok).toBe(true);
        if (!result.ok) return;
        expect(result.request.adoptAccountId).toBeNull();
        expect(result.request.newAccount).toEqual({
            name: 'Mortgage A',
            code: '2400',
            openingBalance: 25_000_000,
            openingDate: '2026-01-01',
        });
        expect(result.request.ratePeriods[0]?.fixedUntil).toBe('2036-01-01');
    });

    it('requires both a name and a code for a fresh account', () => {
        const base = { mode: 'new' as const, adoptAccountId: null, newCode: '2400' };
        expect(buildPartRequest(part({ ...base, newName: '' }), 2).ok).toBe(false);
        expect(buildPartRequest(part({ ...base, newName: 'A', newCode: '' }), 2).ok).toBe(false);
    });
});

describe('buildDeposit', () => {
    const asset = asAccountId('22222222-2222-2222-2222-222222222222');
    const income = asAccountId('33333333-3333-3333-3333-333333333333');

    function deposit(overrides: Partial<DepositDraft> = {}): DepositDraft {
        return { accountId: null, incomeAccountId: null, ratePercent: '', ...overrides };
    }

    it('returns nulls when nothing is set (deposit is optional)', () => {
        const result = buildDeposit(deposit());
        expect(result.ok).toBe(true);
        if (!result.ok) return;
        expect(result.value).toEqual({
            constructionDepositAccountId: null,
            constructionDepositInterestIncomeAccountId: null,
            constructionDepositAnnualRatePercent: null,
        });
    });

    it('rejects a partially-filled deposit (all three fields go together)', () => {
        expect(buildDeposit(deposit({ accountId: asset })).ok).toBe(false);
        expect(buildDeposit(deposit({ accountId: asset, incomeAccountId: income })).ok).toBe(false);
    });

    it('accepts all three fields and rejects an out-of-range rate', () => {
        const ok = buildDeposit(
            deposit({ accountId: asset, incomeAccountId: income, ratePercent: '1.5' }),
        );
        expect(ok.ok).toBe(true);
        if (ok.ok) {
            expect(ok.value).toEqual({
                constructionDepositAccountId: asset,
                constructionDepositInterestIncomeAccountId: income,
                constructionDepositAnnualRatePercent: 1.5,
            });
        }
        const bad = buildDeposit(
            deposit({ accountId: asset, incomeAccountId: income, ratePercent: '150' }),
        );
        expect(bad.ok).toBe(false);
    });
});
