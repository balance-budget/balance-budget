import { msg, t } from '@lingui/core/macro';
import { type RepaymentType, type WireCreateLoanPartRequest } from '../api/loans';
import { todayIso } from '../lib/dates';
import type { AccountId } from '../lib/domain';
import { parseMoney } from '../lib/money';

export const REPAYMENT_TYPES = [
    { id: 'Annuity', label: msg`Annuity` },
    { id: 'Linear', label: msg`Linear` },
    { id: 'InterestOnly', label: msg`Interest-only` },
] satisfies { id: RepaymentType; label: ReturnType<typeof msg> }[];

export type PartDraft = {
    id: string;
    label: string;
    repaymentType: RepaymentType;
    startDate: string;
    endDate: string;
    mode: 'adopt' | 'new';
    adoptAccountId: AccountId | null;
    newName: string;
    newCode: string;
    openingBalance: string;
    ratePercent: string;
    fixedUntil: string;
};

let partCounter = 0;
export function emptyPart(index: number): PartDraft {
    partCounter += 1;
    return {
        id: `part-${partCounter.toString(36)}`,
        label: t`Part ${(index + 1).toString()}`,
        repaymentType: 'Annuity',
        startDate: todayIso(),
        endDate: '',
        mode: 'adopt',
        adoptAccountId: null,
        newName: '',
        newCode: '',
        openingBalance: '',
        ratePercent: '',
        fixedUntil: '',
    };
}

export type BuildPartResult =
    { ok: true; request: WireCreateLoanPartRequest } | { ok: false; error: string };

export function buildPartRequest(part: PartDraft, scale: number): BuildPartResult {
    if (part.label.trim() === '') return { ok: false, error: t`Every part needs a label.` };

    if (part.endDate === '') return { ok: false, error: t`Every part needs an end date.` };

    const rate = Number(part.ratePercent.trim());
    if (part.ratePercent.trim() === '' || !Number.isFinite(rate) || rate < 0) {
        return { ok: false, error: t`Enter a valid annual rate (e.g. 3.8).` };
    }

    let adoptAccountId: AccountId | null = null;
    let newAccount: WireCreateLoanPartRequest['newAccount'] = null;
    if (part.mode === 'adopt') {
        if (part.adoptAccountId === null) {
            return { ok: false, error: t`Pick the liability account to adopt.` };
        }
        adoptAccountId = part.adoptAccountId;
    } else {
        if (part.newName.trim() === '' || part.newCode.trim() === '') {
            return { ok: false, error: t`A fresh part account needs a name and a code.` };
        }
        const opening = parseMoney(
            part.openingBalance.trim() === '' ? '0' : part.openingBalance,
            scale,
        );
        if (!opening.ok || opening.minor < 0) {
            return { ok: false, error: t`Enter a valid opening balance.` };
        }
        newAccount = {
            name: part.newName.trim(),
            code: part.newCode.trim(),
            openingBalance: opening.minor,
            openingDate: part.startDate,
        };
    }

    return {
        ok: true,
        request: {
            label: part.label.trim(),
            repaymentType: part.repaymentType,
            startDate: part.startDate,
            endDate: part.endDate,
            adoptAccountId,
            newAccount,
            ratePeriods: [
                {
                    effectiveDate: part.startDate,
                    annualRatePercent: rate,
                    fixedUntil: part.fixedUntil === '' ? null : part.fixedUntil,
                },
            ],
        },
    };
}

export type DepositDraft = {
    accountId: AccountId | null;
    incomeAccountId: AccountId | null;
    ratePercent: string;
};

export function emptyDeposit(): DepositDraft {
    return { accountId: null, incomeAccountId: null, ratePercent: '' };
}

export type DepositRequestFields = {
    constructionDepositAccountId: AccountId | null;
    constructionDepositInterestIncomeAccountId: AccountId | null;
    constructionDepositAnnualRatePercent: number | null;
};

export type BuildDepositResult =
    { ok: true; value: DepositRequestFields } | { ok: false; error: string };

// The Construction deposit is optional, but all three fields go together (ADR-0026).
export function buildDeposit(d: DepositDraft): BuildDepositResult {
    const anySet =
        d.accountId !== null || d.incomeAccountId !== null || d.ratePercent.trim() !== '';
    const allSet =
        d.accountId !== null && d.incomeAccountId !== null && d.ratePercent.trim() !== '';

    if (!anySet) {
        return {
            ok: true,
            value: {
                constructionDepositAccountId: null,
                constructionDepositInterestIncomeAccountId: null,
                constructionDepositAnnualRatePercent: null,
            },
        };
    }
    if (!allSet) {
        return {
            ok: false,
            error: t`A construction deposit needs an asset account, an income account, and a rate. Set all three or none.`,
        };
    }

    const rate = Number(d.ratePercent.trim());
    if (!Number.isFinite(rate) || rate < 0 || rate > 100) {
        return { ok: false, error: t`Enter a valid deposit rate (0–100).` };
    }
    return {
        ok: true,
        value: {
            constructionDepositAccountId: d.accountId,
            constructionDepositInterestIncomeAccountId: d.incomeAccountId,
            constructionDepositAnnualRatePercent: rate,
        },
    };
}
