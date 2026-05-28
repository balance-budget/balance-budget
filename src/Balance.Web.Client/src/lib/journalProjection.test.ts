import { describe, expect, it } from 'vitest';
import { asAccountId, asJournalLineId, type AccountId, type AccountType } from './domain';
import { projectJournalEntry, type ProjectionLine } from './journalProjection';

// Account ids used across cases. Distinct UUID-shaped strings; the branded type
// guards against parameter-swap when the projection consumes them as keys.
const Checking = asAccountId('11111111-1111-1111-1111-111111111111');
const Savings = asAccountId('22222222-2222-2222-2222-222222222222');
const Groceries = asAccountId('33333333-3333-3333-3333-333333333333');
const Household = asAccountId('44444444-4444-4444-4444-444444444444');
const Toiletries = asAccountId('55555555-5555-5555-5555-555555555555');
const Salary = asAccountId('66666666-6666-6666-6666-666666666666');
const CreditCard = asAccountId('77777777-7777-7777-7777-777777777777');
const Mortgage = asAccountId('88888888-8888-8888-8888-888888888888');
const OpeningBalance = asAccountId('99999999-9999-9999-9999-999999999999');
const TaxWithheld = asAccountId('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
const Bonus = asAccountId('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');

const accountNames: ReadonlyMap<AccountId, string> = new Map([
    [Checking, 'Checking'],
    [Savings, 'Savings'],
    [Groceries, 'Groceries'],
    [Household, 'Household'],
    [Toiletries, 'Toiletries'],
    [Salary, 'Salary'],
    [CreditCard, 'Credit Card'],
    [Mortgage, 'Mortgage'],
    [OpeningBalance, 'Opening Balance'],
    [TaxWithheld, 'Tax Withheld'],
    [Bonus, 'Bonus'],
]);

const CURRENCY = 'EUR';

let lineCounter = 0;
function line(accountId: AccountId, accountType: AccountType, amount: number): ProjectionLine {
    lineCounter += 1;
    const idHex = lineCounter.toString(16).padStart(12, '0');
    return {
        id: asJournalLineId(`00000000-0000-0000-0000-${idHex}`),
        accountId,
        accountName: accountNames.get(accountId) ?? 'Unknown',
        accountType,
        amount,
    };
}

describe('projectJournalEntry', () => {
    it('Asset_to_Expense_is_loss_simplifiable', () => {
        const result = projectJournalEntry(
            [line(Checking, 'Asset', -4000), line(Groceries, 'Expense', 4000)],
            CURRENCY,
        );

        expect(result.netWorthChange).toEqual({ amount: -4000, currencyCode: CURRENCY });
        expect(result.isTransfer).toBe(false);
        expect(result.grossMagnitude).toEqual({ amount: 4000, currencyCode: CURRENCY });
        expect(result.isSimplifiable).toBe(true);
        expect(result.fromLegs.map(l => l.accountId)).toEqual([Checking]);
        expect(result.toLegs.map(l => l.accountId)).toEqual([Groceries]);
    });

    it('Income_to_Asset_is_gain_simplifiable', () => {
        const result = projectJournalEntry(
            [line(Checking, 'Asset', 250000), line(Salary, 'Income', -250000)],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(250000);
        expect(result.isTransfer).toBe(false);
        expect(result.grossMagnitude.amount).toBe(250000);
        expect(result.isSimplifiable).toBe(true);
        expect(result.fromLegs.map(l => l.accountId)).toEqual([Salary]);
        expect(result.toLegs.map(l => l.accountId)).toEqual([Checking]);
    });

    it('Asset_to_Asset_transfer_is_zero_change', () => {
        const result = projectJournalEntry(
            [line(Checking, 'Asset', -100000), line(Savings, 'Asset', 100000)],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(0);
        expect(result.isTransfer).toBe(true);
        expect(result.grossMagnitude.amount).toBe(100000);
        expect(result.isSimplifiable).toBe(true);
        expect(result.fromLegs.map(l => l.accountId)).toEqual([Checking]);
        expect(result.toLegs.map(l => l.accountId)).toEqual([Savings]);
    });

    it('Asset_to_Liability_credit_card_payment_is_zero_change', () => {
        const result = projectJournalEntry(
            [line(Checking, 'Asset', -20000), line(CreditCard, 'Liability', 20000)],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(0);
        expect(result.isTransfer).toBe(true);
        expect(result.grossMagnitude.amount).toBe(20000);
        expect(result.isSimplifiable).toBe(true);
    });

    it('Liability_to_Asset_loan_disbursement_is_zero_change', () => {
        const result = projectJournalEntry(
            [line(Checking, 'Asset', 500000), line(Mortgage, 'Liability', -500000)],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(0);
        expect(result.isTransfer).toBe(true);
        expect(result.grossMagnitude.amount).toBe(500000);
        expect(result.isSimplifiable).toBe(true);
    });

    it('Equity_to_Asset_opening_balance_is_gain', () => {
        const result = projectJournalEntry(
            [line(Checking, 'Asset', 100000), line(OpeningBalance, 'Equity', -100000)],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(100000);
        expect(result.isTransfer).toBe(false);
        expect(result.grossMagnitude.amount).toBe(100000);
        expect(result.isSimplifiable).toBe(true);
    });

    it('One_source_N_destinations_split_is_simplifiable_on_credit_side', () => {
        const result = projectJournalEntry(
            [
                line(Checking, 'Asset', -10000),
                line(Groceries, 'Expense', 6000),
                line(Household, 'Expense', 2500),
                line(Toiletries, 'Expense', 1500),
            ],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(-10000);
        expect(result.isTransfer).toBe(false);
        expect(result.grossMagnitude.amount).toBe(10000);
        expect(result.isSimplifiable).toBe(true);
        expect(result.fromLegs.map(l => l.accountId)).toEqual([Checking]);
        // Debit side: ordered by accountName ordinal — Groceries, Household, Toiletries.
        expect(result.toLegs.map(l => l.accountId)).toEqual([Groceries, Household, Toiletries]);
    });

    it('N_sources_one_destination_split_is_simplifiable_on_debit_side', () => {
        const result = projectJournalEntry(
            [
                line(Mortgage, 'Liability', 100000),
                line(Checking, 'Asset', -60000),
                line(Savings, 'Asset', -40000),
            ],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(0);
        expect(result.isTransfer).toBe(true);
        expect(result.grossMagnitude.amount).toBe(100000);
        expect(result.isSimplifiable).toBe(true);
        // Credit side: Checking, Savings ordered alphabetically.
        expect(result.fromLegs.map(l => l.accountId)).toEqual([Checking, Savings]);
        expect(result.toLegs.map(l => l.accountId)).toEqual([Mortgage]);
    });

    it('Multi_source_multi_destination_is_not_simplifiable', () => {
        const result = projectJournalEntry(
            [
                line(Salary, 'Income', -300000),
                line(Bonus, 'Income', -50000),
                line(TaxWithheld, 'Expense', 100000),
                line(Checking, 'Asset', 250000),
            ],
            CURRENCY,
        );

        expect(result.netWorthChange.amount).toBe(250000);
        expect(result.isTransfer).toBe(false);
        expect(result.grossMagnitude.amount).toBe(350000);
        expect(result.isSimplifiable).toBe(false);
        expect(result.fromLegs).toHaveLength(0);
        expect(result.toLegs).toHaveLength(0);
    });

    it('Gross_magnitude_equals_sum_of_debits', () => {
        const result = projectJournalEntry(
            [
                line(Checking, 'Asset', -7500),
                line(Groceries, 'Expense', 3000),
                line(Household, 'Expense', 4500),
            ],
            CURRENCY,
        );

        expect(result.grossMagnitude.amount).toBe(7500);
    });

    it('Empty_lines_returns_defaults', () => {
        const result = projectJournalEntry([], CURRENCY);

        expect(result.netWorthChange.amount).toBe(0);
        expect(result.isTransfer).toBe(true);
        expect(result.grossMagnitude.amount).toBe(0);
        expect(result.isSimplifiable).toBe(false);
        expect(result.fromLegs).toHaveLength(0);
        expect(result.toLegs).toHaveLength(0);
    });
});
