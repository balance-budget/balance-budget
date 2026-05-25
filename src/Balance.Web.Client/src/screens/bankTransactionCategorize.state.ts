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

import type { components } from '../lib/api-types';
import type { AccountId, CounterpartyId } from '../lib/domain';
import { parseMoney } from '../lib/money';
import type { SuggestedCounterAccount } from '../api/counterparties';

type WireCategorizeRequest = components['schemas']['CategorizeBankTransactionRequest'];

export type CounterpartyMode = 'existing' | 'new';

export type LineInput = {
    id: string;
    accountId: AccountId | null;
    amount: string;
    description: string;
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

export function initialForm(args: {
    today: string;
    bookingDate: string;
    description: string;
    resolvedCounterpartyId: CounterpartyId | null;
}): CategorizeFormState {
    return {
        date: args.bookingDate || args.today,
        description: args.description,
        counterpartyMode: 'existing',
        counterpartyId: args.resolvedCounterpartyId,
        newCounterpartyName: '',
        lines: [emptyLine()],
    };
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
        if (projection.minor !== null) allocated += projection.minor;
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

    if (form.counterpartyMode === 'existing' && form.counterpartyId === null) {
        errors.counterpartyId = ['Pick a counterparty or create a new one.'];
    }
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
        allocated += projection.minor;
        if (line.accountId !== null) {
            projected.push({
                accountId: line.accountId,
                amount: counterSign * projection.minor,
                description: line.description.trim() === '' ? null : line.description.trim(),
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
        form.counterpartyMode === 'new'
            ? { name: form.newCounterpartyName.trim() }
            : null;
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
