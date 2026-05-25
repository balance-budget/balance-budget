import { describe, expect, it } from 'vitest';
import { asAccountId, asCounterpartyId } from '../lib/domain';
import { buildRequest, initialForm } from './bankTransactionCategorize.state';

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
            btAmountMinor: 250_000,
            formatMagnitude: formatTwoDecimals,
        });

        expect(form.lines[0]?.amount).toBe('2500.00');
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
