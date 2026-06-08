/*
 * Pure form-state helpers for the Bank transaction categorisation form. Keeps
 * the user-input ↔ wire-shape projection out of the React component.
 *
 * Sign rule on the wire (mirrors the server-side rule in
 * `BankTransactionCategorisationService`): the bank-side line uses
 * `+BT.Money.Amount`, so the counter-side lines must sum to `-BT.Money.Amount`.
 * The form takes positive magnitudes from the user — the sign is determined
 * once at projection time from the BT direction.
 */

import type { components } from '../lib/api-types.gen';
import type { AccountId, CounterpartyId, LoanPartId } from '../lib/domain';
import { parseMoney } from '../lib/money';
import type { BankAccount } from '../api/bankAccounts';
import type { SuggestedCounterAccount } from '../api/counterparties';
import type { LoanProposal } from '../api/loans';

type WireCategorizeRequest = components['schemas']['CategorizeBankTransactionRequest'];

export type CounterpartyMode = 'existing' | 'new';

export type LineInput = {
    id: string;
    accountId: AccountId | null;
    amount: string;
    description: string;
    /** Loan Part attribution — set only by the loan-aware mode (ADR-0025). */
    loanPartId?: LoanPartId | null;
    /**
     * A credit line on the *opposite* side from the other counter lines — the
     * deposit-interest offset (ADR-0026): an Income credit that lowers the total
     * so it matches the netted bank debit. The magnitude is still entered
     * positive; this flips its sign at projection time.
     */
    credit?: boolean;
};

export type CategorizeFormState = {
    date: string;
    description: string;
    counterpartyMode: CounterpartyMode;
    counterpartyId: CounterpartyId | null;
    newCounterpartyName: string;
    lines: LineInput[];
};

export type FieldErrors = Record<string, string[]>;

export type BuildRequestResult =
    | { ok: true; request: WireCategorizeRequest }
    | { ok: false; fieldErrors: FieldErrors; topError?: string };

let counter = 0;
export function nextLineId(): string {
    counter += 1;
    return `cat-${counter.toString(36)}-${Date.now().toString(36)}`;
}

export function emptyLine(): LineInput {
    return { id: nextLineId(), accountId: null, amount: '', description: '' };
}

/** Format an absolute minor-units amount as a positive major-units string for
 *  use in the magnitude input. Mirrors the form's input convention (positive
 *  magnitudes only — sign is derived at projection time from BT direction). */
export function formatMagnitudeFor(scale: number): (minor: number) => string {
    return (minor: number): string => {
        const absMinor = Math.abs(minor);
        const divisor = 10 ** scale;
        const major = Math.floor(absMinor / divisor);
        const remainder = absMinor - major * divisor;
        if (scale === 0) return major.toString();
        return `${major.toString()}.${remainder.toString().padStart(scale, '0')}`;
    };
}

export function initialForm(args: {
    today: string;
    bookingDate: string;
    description: string;
    resolvedCounterpartyId: CounterpartyId | null;
    prefilledAccountId: AccountId | null;
    btAmountMinor: number;
    formatMagnitude: (minor: number) => string;
}): CategorizeFormState {
    const seedLine: LineInput = {
        ...emptyLine(),
        accountId: args.prefilledAccountId,
        amount: args.formatMagnitude(args.btAmountMinor),
    };
    return {
        date: args.bookingDate || args.today,
        description: args.description,
        counterpartyMode: 'existing',
        counterpartyId: args.resolvedCounterpartyId,
        newCounterpartyName: '',
        lines: [seedLine],
    };
}

/**
 * Resolve the "open BT" projection per ADR 0013 step 1 / step 2.
 *
 *   1. If the BT's CounterpartyAccountNumber exact-matches a BankAccount whose
 *      AccountId IS NOT NULL, this is a *self-transfer in progress*: the
 *      counter-side line is pre-filled with that own-Account and the form
 *      submits with CounterpartyId = null.
 *   2. Else if the matched BankAccount has CounterpartyId IS NOT NULL, the
 *      counterparty is identified and the suggestions effect supplies the
 *      last-used counter-side Account(s) (existing behaviour).
 *   3. Otherwise (null IBAN, no match, or a BankAccount with neither side
 *      set) the resolver returns `none` and the form opens empty.
 *
 * Whitespace and casing are normalised before comparison to match the import
 * paths' lenient IBAN handling.
 */
export type OpenContext =
    | { kind: 'self-transfer'; prefilledAccountId: AccountId }
    | { kind: 'counterparty'; counterpartyId: CounterpartyId }
    | { kind: 'none' };

export function resolveOpenContext(
    counterpartyAccountNumber: string | null,
    bankAccounts: readonly BankAccount[],
): OpenContext {
    if (counterpartyAccountNumber === null) return { kind: 'none' };
    const normalised = counterpartyAccountNumber.replace(/\s+/g, '').toUpperCase();
    if (normalised.length === 0) return { kind: 'none' };

    // Two passes: self-transfer wins over a same-IBAN counterparty row if both
    // exist (ADR 0010 keeps them mutually exclusive on one BankAccount, but
    // we don't want the order of `bankAccounts` deciding which side wins).
    let counterpartyCandidate: CounterpartyId | null = null;
    for (const ba of bankAccounts) {
        if (!ba.iban) continue;
        if (ba.iban.replace(/\s+/g, '').toUpperCase() !== normalised) continue;
        if (ba.accountId !== null) {
            return { kind: 'self-transfer', prefilledAccountId: ba.accountId };
        }
        if (ba.counterpartyId !== null && counterpartyCandidate === null) {
            counterpartyCandidate = ba.counterpartyId;
        }
    }
    return counterpartyCandidate === null
        ? { kind: 'none' }
        : { kind: 'counterparty', counterpartyId: counterpartyCandidate };
}

/**
 * Pre-fill lines from the suggested counter-side accounts. When the suggestion
 * is a single account, the full BT magnitude is used. For splits, the prior
 * proportions are scaled to the current BT magnitude (e.g. a prior 70/30
 * 60€/27.43€ split becomes a 70/30 split of whatever amount the new BT has).
 * Rounding drift accumulates on the last line.
 */
export function applySuggestionsToLines(
    suggestions: readonly SuggestedCounterAccount[],
    btAmountMinor: number,
    formatMagnitude: (minor: number) => string,
): LineInput[] {
    if (suggestions.length === 0) return [emptyLine()];

    const target = Math.abs(btAmountMinor);
    const priorTotal = suggestions.reduce((sum, s) => sum + Math.abs(s.amount), 0);

    if (suggestions.length === 1 || priorTotal === 0) {
        return suggestions.map((s, i) => ({
            id: nextLineId(),
            accountId: s.accountId,
            amount: i === 0 ? formatMagnitude(target) : '0',
            description: '',
        }));
    }

    const scaled: number[] = suggestions.map(s =>
        Math.round((Math.abs(s.amount) / priorTotal) * target),
    );
    const drift = target - scaled.reduce((a, b) => a + b, 0);
    if (scaled.length > 0) {
        scaled[scaled.length - 1] = (scaled[scaled.length - 1] ?? 0) + drift;
    }
    return suggestions.map((s, i) => ({
        id: nextLineId(),
        accountId: s.accountId,
        amount: formatMagnitude(scaled[i] ?? 0),
        description: '',
    }));
}

/**
 * Pre-fill lines from the loan-aware payment proposal (ADR-0025): one
 * principal line per included amortizing part (on the part's account) and one
 * interest line per included part (on the loan's interest Expense account),
 * each attributed to its Loan Part. Engine amounts are editable defaults —
 * the bank's actual charge wins. Zero proposal amounts are still rendered so
 * a rounding-different bank charge has a line ready to edit, except principal
 * lines for interest-only parts (always zero by construction).
 */
export function linesFromLoanProposal(
    proposal: LoanProposal,
    includedPartIds: ReadonlySet<string> | null,
    formatMagnitude: (minor: number) => string,
): LineInput[] {
    const lines: LineInput[] = [];
    for (const part of proposal.lines) {
        if (includedPartIds !== null && !includedPartIds.has(part.loanPartId)) continue;

        if (part.principal !== 0) {
            lines.push({
                id: nextLineId(),
                accountId: part.partAccountId,
                amount: formatMagnitude(part.principal),
                description: `${part.label} — principal`,
                loanPartId: part.loanPartId,
            });
        }
        lines.push({
            id: nextLineId(),
            accountId: proposal.interestExpenseAccountId,
            amount: formatMagnitude(part.interest),
            description: `${part.label} — interest`,
            loanPartId: part.loanPartId,
        });
    }

    // Construction deposit interest offset (ADR-0026): an Income credit that nets
    // the gross interest down to the bank's actual debit during construction.
    if (proposal.depositOffset && proposal.depositOffset.amount > 0 && lines.length > 0) {
        lines.push({
            id: nextLineId(),
            accountId: proposal.depositOffset.incomeAccountId,
            amount: formatMagnitude(proposal.depositOffset.amount),
            description: 'Construction deposit interest offset',
            credit: true,
        });
    }

    return lines.length === 0 ? [emptyLine()] : lines;
}

export type LineProjection = { minor: number | null; error: string | null };

export function projectLine(line: LineInput, scale: number): LineProjection {
    if (line.accountId === null && line.amount.trim() === '') {
        return { minor: null, error: null };
    }
    const parsed = parseMoney(line.amount, scale);
    if (!parsed.ok) {
        return { minor: null, error: parsed.error };
    }
    if (parsed.minor < 0) {
        return { minor: null, error: 'Enter a positive amount' };
    }
    return { minor: parsed.minor, error: null };
}

export type TotalsState = {
    targetMinor: number;
    allocatedMinor: number;
    unallocatedMinor: number;
    balanced: boolean;
};

export function computeTotals(
    lines: LineInput[],
    btAmountMinor: number,
    scale: number,
): TotalsState {
    const target = Math.abs(btAmountMinor);
    let allocated = 0;
    for (const line of lines) {
        const projection = projectLine(line, scale);
        if (projection.minor !== null) allocated += (line.credit ? -1 : 1) * projection.minor;
    }
    return {
        targetMinor: target,
        allocatedMinor: allocated,
        unallocatedMinor: target - allocated,
        balanced: allocated === target && target > 0,
    };
}

export function buildRequest(
    form: CategorizeFormState,
    btAmountMinor: number,
    scale: number,
): BuildRequestResult {
    const errors: FieldErrors = {};
    let topError: string | undefined;

    if (form.date.trim() === '') {
        errors.date = ['Required'];
    }

    // 'existing' mode with a null counterpartyId is a valid self-transfer
    // (CONTEXT.md, ADR 0013(e)) — the server treats both inputs null as
    // "no external party", so we don't require a pick here either.
    if (form.counterpartyMode === 'new') {
        const trimmed = form.newCounterpartyName.trim();
        if (trimmed.length === 0) {
            errors['newCounterparty.name'] = ['Required'];
        } else if (trimmed.length > 200) {
            errors['newCounterparty.name'] = ['At most 200 characters'];
        }
    }

    const counterSign = btAmountMinor < 0 ? 1 : -1;
    const projected: WireCategorizeRequest['lines'] = [];
    let allocated = 0;
    let nonEmptyCount = 0;

    form.lines.forEach((line, i) => {
        const empty = line.accountId === null && line.amount.trim() === '';
        if (empty) return;
        nonEmptyCount += 1;
        if (line.accountId === null) {
            errors[`lines[${i.toString()}].accountId`] = ['Required'];
        }
        const projection = projectLine(line, scale);
        if (projection.error) {
            errors[`lines[${i.toString()}].amount`] = [projection.error];
            return;
        }
        if (projection.minor === null || projection.minor <= 0) {
            errors[`lines[${i.toString()}].amount`] = ['Enter an amount greater than zero'];
            return;
        }
        const lineSign = line.credit ? -1 : 1;
        allocated += lineSign * projection.minor;
        if (line.accountId !== null) {
            projected.push({
                accountId: line.accountId,
                amount: counterSign * lineSign * projection.minor,
                description: line.description.trim() === '' ? null : line.description.trim(),
                loanPartId: line.loanPartId ?? null,
            });
        }
    });

    if (nonEmptyCount === 0) {
        errors.lines = ['Add at least one line.'];
    } else if (Object.keys(errors).length === 0) {
        const target = Math.abs(btAmountMinor);
        if (allocated !== target) {
            const diff = target - allocated;
            topError =
                diff > 0
                    ? `Lines are under-allocated. Allocate the remaining amount.`
                    : `Lines are over-allocated. Reduce one or more amounts.`;
        }
    }

    if (Object.keys(errors).length > 0 || topError !== undefined) {
        return { ok: false, fieldErrors: errors, topError };
    }

    const counterpartyId = form.counterpartyMode === 'existing' ? form.counterpartyId : null;
    const newCounterparty =
        form.counterpartyMode === 'new' ? { name: form.newCounterpartyName.trim() } : null;
    const trimmedDescription = form.description.trim();

    return {
        ok: true,
        request: {
            counterpartyId,
            newCounterparty,
            date: form.date,
            description: trimmedDescription === '' ? null : trimmedDescription,
            lines: projected,
        },
    };
}
