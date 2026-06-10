/*
 * Pure form-state helpers for `/journal/new`. Keeps the Simple ↔ Advanced
 * conversion + wire-shape projection out of the React component so they can
 * be unit-tested independently once frontend test infra lands.
 *
 * Simple shape is the personal-finance "money moved from N sources to M
 * destinations" framing. Advanced is the bookkeeping general-journal table
 * with explicit Debit / Credit columns. Both render the same underlying
 * line set; switching modes is bidirectional when the line set is 1-to-N
 * or N-to-1 — otherwise `tryAdvancedToSimple` returns null and the toggle
 * back to Simple is disabled.
 *
 * Sign rule on the wire (matches the server-side JournalLine.Amount
 * convention from JournalEntryValidator):
 *   - "From" leg in Simple becomes Amount = -minor (credit).
 *   - "To"   leg in Simple becomes Amount = +minor (debit).
 *   - Advanced row becomes Amount = debitMinor - creditMinor (exactly one of
 *     debit / credit is non-zero per row, enforced by validation).
 */

import { t } from '@lingui/core/macro';
import type { components } from '../lib/api-types.gen';
import type { AccountId, AccountType, CounterpartyId } from '../lib/domain';
import { parseMoney, type ParseMoneyResult } from '../lib/money';

type WireCreateRequest = components['schemas']['CreateJournalEntryRequest'];

export type FormMode = 'simple' | 'advanced';

export type SimpleLeg = {
    id: string;
    accountId: AccountId | null;
    amount: string;
};

export type AdvancedLine = {
    id: string;
    accountId: AccountId | null;
    debit: string;
    credit: string;
    description: string;
};

export type FormHeader = {
    date: string;
    description: string;
    counterpartyId: CounterpartyId | null;
};

export type FormState = {
    mode: FormMode;
    header: FormHeader;
    simple: { from: SimpleLeg[]; to: SimpleLeg[] };
    advanced: AdvancedLine[];
};

export type FieldErrors = Record<string, string[]>;

export type BuildRequestResult =
    | { ok: true; request: WireCreateRequest }
    | { ok: false; fieldErrors: FieldErrors; topError?: string };

let counter = 0;
export function nextLineId(): string {
    counter += 1;
    return `l-${counter.toString(36)}-${Date.now().toString(36)}`;
}

export function emptySimpleLeg(): SimpleLeg {
    return { id: nextLineId(), accountId: null, amount: '' };
}

export function emptyAdvancedLine(): AdvancedLine {
    return { id: nextLineId(), accountId: null, debit: '', credit: '', description: '' };
}

export function emptyForm(today: string): FormState {
    return {
        mode: 'simple',
        header: { date: today, description: '', counterpartyId: null },
        simple: { from: [emptySimpleLeg()], to: [emptySimpleLeg()] },
        advanced: [emptyAdvancedLine(), emptyAdvancedLine()],
    };
}

/** The Simple column a prefilled account lands in, by normal balance: debit-normal
 *  types (Asset, Expense) usually receive money ("To"), credit-normal types
 *  (Liability, Equity, Income) usually provide it ("From"). The user can move it. */
export function prefillSide(type: AccountType): 'from' | 'to' {
    return type === 'Asset' || type === 'Expense' ? 'to' : 'from';
}

/** An empty form with one Simple leg preselected to `account` on its normal-balance side. */
export function prefilledForm(
    today: string,
    account: { id: AccountId; type: AccountType },
): FormState {
    const base = emptyForm(today);
    const side = prefillSide(account.type);
    return {
        ...base,
        simple: {
            ...base.simple,
            [side]: [{ ...emptySimpleLeg(), accountId: account.id }],
        },
    };
}

export type ScaleLookup = (accountId: AccountId) => number | null;

/** Project Simple legs into the same shape Advanced uses, preserving ids so
 *  React keys stay stable across toggles. Empty amounts stay empty (Advanced
 *  picks up the debit/credit column based on which side the leg came from). */
export function simpleToAdvanced(simple: FormState['simple']): AdvancedLine[] {
    const lines: AdvancedLine[] = [];
    for (const leg of simple.from) {
        lines.push({
            id: leg.id,
            accountId: leg.accountId,
            debit: '',
            credit: leg.amount,
            description: '',
        });
    }
    for (const leg of simple.to) {
        lines.push({
            id: leg.id,
            accountId: leg.accountId,
            debit: leg.amount,
            credit: '',
            description: '',
        });
    }
    return lines;
}

/** Project Advanced lines back into Simple legs. Rows with both debit and
 *  credit set are genuinely ambiguous (Simple can't express them) and abort
 *  the conversion with null. Empty rows and account-only rows are dropped —
 *  the empty-side pad at the end restores a canonical From/To pair so a
 *  fresh form (no input typed yet) round-trips cleanly. */
export function tryAdvancedToSimple(lines: AdvancedLine[]): FormState['simple'] | null {
    const from: SimpleLeg[] = [];
    const to: SimpleLeg[] = [];
    for (const line of lines) {
        const hasDebit = line.debit.trim().length > 0;
        const hasCredit = line.credit.trim().length > 0;
        if (hasDebit && hasCredit) {
            return null;
        }
        if (!hasDebit && !hasCredit) {
            continue;
        }
        if (hasDebit) {
            to.push({ id: line.id, accountId: line.accountId, amount: line.debit });
        } else {
            from.push({ id: line.id, accountId: line.accountId, amount: line.credit });
        }
    }
    if (from.length === 0) from.push(emptySimpleLeg());
    if (to.length === 0) to.push(emptySimpleLeg());
    return { from, to };
}

function addFieldError(errors: FieldErrors, key: string, message: string): void {
    const existing = errors[key];
    if (existing) {
        existing.push(message);
    } else {
        errors[key] = [message];
    }
}

function requireAccount(
    accountId: AccountId | null,
    key: string,
    errors: FieldErrors,
): AccountId | null {
    if (accountId === null) {
        addFieldError(errors, key, t`Select an account.`);
        return null;
    }
    return accountId;
}

function parseAmount(
    raw: string,
    accountId: AccountId | null,
    scaleLookup: ScaleLookup,
    key: string,
    errors: FieldErrors,
): number | null {
    if (accountId === null) {
        return null;
    }
    const scale = scaleLookup(accountId);
    if (scale === null) {
        return null;
    }
    const parsed: ParseMoneyResult = parseMoney(raw, scale);
    if (!parsed.ok) {
        addFieldError(errors, key, parsed.error);
        return null;
    }
    if (parsed.minor === 0) {
        addFieldError(errors, key, t`Amount must be non-zero.`);
        return null;
    }
    return parsed.minor;
}

function headerWire(
    header: FormHeader,
): Pick<WireCreateRequest, 'date' | 'description' | 'counterpartyId'> {
    const trimmed = header.description.trim();
    return {
        date: header.date,
        description: trimmed.length === 0 ? null : trimmed,
        counterpartyId: header.counterpartyId,
    };
}

/** Build the wire request from the Simple shape. Errors are collected into
 *  the field-error map under keys that match the FieldError name the form
 *  renders next to each input ("simple.from[0].amount", etc.). */
export function simpleStateToCreateRequest(
    simple: FormState['simple'],
    header: FormHeader,
    scaleLookup: ScaleLookup,
): BuildRequestResult {
    const errors: FieldErrors = {};
    const lines: components['schemas']['CreateJournalLineRequest'][] = [];

    simple.from.forEach((leg, i) => {
        const accountKey = `simple.from[${i}].accountId`;
        const amountKey = `simple.from[${i}].amount`;
        const account = requireAccount(leg.accountId, accountKey, errors);
        const minor = parseAmount(leg.amount, account, scaleLookup, amountKey, errors);
        if (account !== null && minor !== null) {
            lines.push({ accountId: account, amount: -Math.abs(minor), description: null });
        }
    });

    simple.to.forEach((leg, i) => {
        const accountKey = `simple.to[${i}].accountId`;
        const amountKey = `simple.to[${i}].amount`;
        const account = requireAccount(leg.accountId, accountKey, errors);
        const minor = parseAmount(leg.amount, account, scaleLookup, amountKey, errors);
        if (account !== null && minor !== null) {
            lines.push({ accountId: account, amount: Math.abs(minor), description: null });
        }
    });

    if (Object.keys(errors).length > 0) {
        return { ok: false, fieldErrors: errors };
    }

    const fromSum = lines
        .filter(l => Number(l.amount) < 0)
        .reduce((s, l) => s + Number(l.amount), 0);
    const toSum = lines.filter(l => Number(l.amount) > 0).reduce((s, l) => s + Number(l.amount), 0);
    if (fromSum + toSum !== 0) {
        return {
            ok: false,
            fieldErrors: errors,
            topError: t`From and To amounts must balance to zero.`,
        };
    }

    return { ok: true, request: { ...headerWire(header), lines } };
}

/** Build the wire request from the Advanced shape. Same field-error key
 *  convention as Simple ("advanced[0].accountId", "advanced[0].debit", ...). */
export function advancedStateToCreateRequest(
    advanced: AdvancedLine[],
    header: FormHeader,
    scaleLookup: ScaleLookup,
): BuildRequestResult {
    const errors: FieldErrors = {};
    const lines: components['schemas']['CreateJournalLineRequest'][] = [];

    advanced.forEach((line, i) => {
        const accountKey = `advanced[${i}].accountId`;
        const debitKey = `advanced[${i}].debit`;
        const creditKey = `advanced[${i}].credit`;
        const account = requireAccount(line.accountId, accountKey, errors);
        const hasDebit = line.debit.trim().length > 0;
        const hasCredit = line.credit.trim().length > 0;
        if (hasDebit && hasCredit) {
            addFieldError(errors, debitKey, t`Use Debit or Credit, not both.`);
            return;
        }
        if (!hasDebit && !hasCredit) {
            addFieldError(errors, debitKey, t`Enter Debit or Credit.`);
            return;
        }
        const minor = hasDebit
            ? parseAmount(line.debit, account, scaleLookup, debitKey, errors)
            : parseAmount(line.credit, account, scaleLookup, creditKey, errors);
        if (account !== null && minor !== null) {
            const description = line.description.trim();
            lines.push({
                accountId: account,
                amount: hasDebit ? Math.abs(minor) : -Math.abs(minor),
                description: description.length === 0 ? null : description,
            });
        }
    });

    if (Object.keys(errors).length > 0) {
        return { ok: false, fieldErrors: errors };
    }

    const sum = lines.reduce((s, l) => s + Number(l.amount), 0);
    if (sum !== 0) {
        return {
            ok: false,
            fieldErrors: errors,
            topError: t`Debit and Credit totals must balance to zero.`,
        };
    }

    return { ok: true, request: { ...headerWire(header), lines } };
}

/** Live "Σ Debit | Σ Credit" indicator backing the Advanced footer. Skips
 *  lines that fail to parse — the form-level validation surfaces those when
 *  the user submits. */
export type AdvancedTotals = {
    debitMinor: number;
    creditMinor: number;
    balanced: boolean;
};

export function computeAdvancedTotals(
    advanced: AdvancedLine[],
    scaleLookup: ScaleLookup,
): AdvancedTotals {
    let debit = 0;
    let credit = 0;
    for (const line of advanced) {
        if (line.accountId === null) continue;
        const scale = scaleLookup(line.accountId);
        if (scale === null) continue;
        if (line.debit.trim().length > 0) {
            const parsed = parseMoney(line.debit, scale);
            if (parsed.ok) debit += Math.abs(parsed.minor);
        }
        if (line.credit.trim().length > 0) {
            const parsed = parseMoney(line.credit, scale);
            if (parsed.ok) credit += Math.abs(parsed.minor);
        }
    }
    return { debitMinor: debit, creditMinor: credit, balanced: debit === credit && debit > 0 };
}
