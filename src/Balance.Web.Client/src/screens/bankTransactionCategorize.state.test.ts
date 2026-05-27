import { describe, expect, it } from 'vitest';
import type { BankAccount } from '../api/bankAccounts';
import {
    asAccountId,
    asBankAccountId,
    asCounterpartyId,
    type AccountId,
    type CounterpartyId,
} from '../lib/domain';
import {
    buildRequest,
    initialForm,
    resolveOpenContext,
} from './bankTransactionCategorize.state';

const formatTwoDecimals = (minor: number): string => {
    const major = Math.floor(Math.abs(minor) / 100);
    const remainder = Math.abs(minor) - major * 100;
    return `${major.toString()}.${remainder.toString().padStart(2, '0')}`;
};

describe('initialForm', () => {
    it('pre-fills the single line amount with the absolute BT magnitude', () => {
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: 'AH groceries',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: -4200,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.lines).toHaveLength(1);
        expect(form.lines[0]?.amount).toBe('42.00');
        expect(form.lines[0]?.accountId).toBeNull();
    });

    it('uses the booking date when provided', () => {
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: '',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: 100,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.date).toBe('2026-05-20');
    });

    it('falls back to today when booking date is empty', () => {
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '',
            description: '',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: 100,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.date).toBe('2026-05-25');
    });

    it('seeds the resolved counterparty id in existing mode', () => {
        const cp = asCounterpartyId('11111111-1111-1111-1111-111111111111');
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: '',
            resolvedCounterpartyId: cp,
            prefilledAccountId: null,
            btAmountMinor: 100,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.counterpartyMode).toBe('existing');
        expect(form.counterpartyId).toBe(cp);
    });

    it('uses the magnitude of a positive BT (incoming row)', () => {
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: 'Salary',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: 250_000,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.lines[0]?.amount).toBe('2500.00');
    });
});

describe('resolveOpenContext', () => {
    const ownAccountId = asAccountId('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
    const counterpartyId = asCounterpartyId('cccccccc-cccc-cccc-cccc-cccccccccccc');

    function bankAccount(args: {
        id: string;
        iban: string | null;
        accountId?: AccountId | null;
        counterpartyId?: CounterpartyId | null;
    }): BankAccount {
        return {
            id: asBankAccountId(args.id),
            iban: args.iban,
            accountNumber: null,
            bic: null,
            bankName: null,
            accountHolderName: null,
            currencyCode: null,
            accountId: args.accountId ?? null,
            counterpartyId: args.counterpartyId ?? null,
        };
    }

    it('returns self-transfer when matched BankAccount.AccountId is non-null (ADR 0014 step 1)', () => {
        const own = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1',
            iban: 'NL00BANK0000000001',
            accountId: ownAccountId,
        });
        const result = resolveOpenContext('NL00BANK0000000001', [own]);
        expect(result).toEqual({ kind: 'self-transfer', prefilledAccountId: ownAccountId });
    });

    it('returns counterparty when matched BankAccount only has CounterpartyId (fall-through to step 2)', () => {
        const cpBa = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2',
            iban: 'NL00BANK0000000002',
            counterpartyId,
        });
        const result = resolveOpenContext('NL00BANK0000000002', [cpBa]);
        expect(result).toEqual({ kind: 'counterparty', counterpartyId });
    });

    it('returns none when CounterpartyAccountNumber is null (skip self-transfer check cleanly)', () => {
        const own = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1',
            iban: 'NL00BANK0000000001',
            accountId: ownAccountId,
        });
        expect(resolveOpenContext(null, [own])).toEqual({ kind: 'none' });
    });

    it('returns none on IBAN typo (no matching BankAccount)', () => {
        const own = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1',
            iban: 'NL00BANK0000000001',
            accountId: ownAccountId,
        });
        expect(resolveOpenContext('NL00BANK9999999999', [own])).toEqual({ kind: 'none' });
    });

    it('normalises whitespace and case before comparing IBANs', () => {
        const own = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1',
            iban: 'NL00 BANK 0000 0000 01',
            accountId: ownAccountId,
        });
        const result = resolveOpenContext('nl00bank0000000001', [own]);
        expect(result).toEqual({ kind: 'self-transfer', prefilledAccountId: ownAccountId });
    });

    it('prefers self-transfer when an own-account match exists even if a counterparty BA shares the IBAN', () => {
        // Defensive: ADR 0011 keeps AccountId / CounterpartyId mutually exclusive on a
        // single BankAccount, but the resolver must still pick the own-account row when
        // both shapes appear under the same IBAN across rows (e.g. legacy data).
        const cpBa = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2',
            iban: 'NL00BANK0000000003',
            counterpartyId,
        });
        const own = bankAccount({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3',
            iban: 'NL00BANK0000000003',
            accountId: ownAccountId,
        });
        const result = resolveOpenContext('NL00BANK0000000003', [cpBa, own]);
        expect(result).toEqual({ kind: 'self-transfer', prefilledAccountId: ownAccountId });
    });
});

describe('initialForm self-transfer pre-fill', () => {
    it('seeds the line accountId when prefilledAccountId is provided', () => {
        const savings = asAccountId('22222222-2222-2222-2222-222222222222');
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: '',
            resolvedCounterpartyId: null,
            prefilledAccountId: savings,
            btAmountMinor: -25_000,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.lines).toHaveLength(1);
        expect(form.lines[0]?.accountId).toBe(savings);
        expect(form.lines[0]?.amount).toBe('250.00');
        expect(form.counterpartyMode).toBe('existing');
        expect(form.counterpartyId).toBeNull();
    });

    it('leaves the line accountId null when prefilledAccountId is null', () => {
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: '',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: -25_000,
            formatMagnitude: formatTwoDecimals,
        });
        expect(form.lines[0]?.accountId).toBeNull();
    });
});

describe('buildRequest', () => {
    const savings = asAccountId('22222222-2222-2222-2222-222222222222');

    it('accepts a self-transfer (existing mode, null counterpartyId) and emits null on the wire', () => {
        const form = initialForm({
            today: '2026-05-25',
            bookingDate: '2026-05-20',
            description: 'Transfer to savings',
            resolvedCounterpartyId: null,
            prefilledAccountId: null,
            btAmountMinor: -25_000,
            formatMagnitude: formatTwoDecimals,
        });
        const formWithAccount = {
            ...form,
            lines: form.lines.map(l => ({ ...l, accountId: savings })),
        };

        const result = buildRequest(formWithAccount, -25_000, 2);

        expect(result.ok).toBe(true);
        if (result.ok) {
            expect(result.request.counterpartyId).toBeNull();
            expect(result.request.newCounterparty).toBeNull();
            expect(result.request.lines).toHaveLength(1);
            expect(result.request.lines[0]?.amount).toBe(25_000);
        }
    });
});
