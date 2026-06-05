/*
 * Pure form-state helpers for the JournalDetail edit form (ADR 0014). Keeps
 * the user-input ↔ wire-shape projection out of the React component.
 *
 * Editability gate (matches the server-side check in JournalEntryService.ReplaceAsync):
 *  - A line whose `reconciliationStatus === 'Uncleared'` is fully editable
 *    (AccountId, Amount, Description), removable, and the entry accepts new
 *    lines via "Add line" — these default to Uncleared on the wire.
 *  - A line whose status is `Cleared` or `Reconciled` is frozen: AccountId
 *    and Amount must round-trip unchanged; only Description is editable; the
 *    row is not removable.
 *
 * Sign rule on the wire (matches the server-side JournalLine.Amount convention
 * from JournalEntryValidator): per-currency, line amounts sum to zero. The
 * form takes positive magnitudes from the user and a Debit/Credit toggle —
 * frozen lines are projected from their existing signed amount.
 */

import type { components } from '../lib/api-types';
import type { AccountId, JournalLineId } from '../lib/domain';
import { parseMoney } from '../lib/money';

type WireReplaceRequest = components['schemas']['ReplaceJournalEntryRequest'];
type WireReplaceLine = components['schemas']['ReplaceJournalLineRequest'];
type WireReconciliationStatus = components['schemas']['ReconciliationStatus'];

export type EditLineSide = 'debit' | 'credit';

export type EditLine = {
    /** Stable React key. New lines get a synthetic id; existing lines reuse the JournalLineId. */
    key: string;
    /** Server-assigned JournalLineId for existing lines; null on freshly-added lines. */
    serverId: JournalLineId | null;
    /** ReconciliationStatus from the loaded entry — drives the editability gate. New lines are Uncleared. */
    status: WireReconciliationStatus;
    accountId: AccountId | null;
    side: EditLineSide;
    amount: string;
    description: string;
};

export type FieldErrors = Record<string, string[]>;

export type BuildReplaceResult =
    | { ok: true; request: WireReplaceRequest }
    | { ok: false; fieldErrors: FieldErrors; topError?: string };

let counter = 0;
export function nextLineKey(): string {
    counter += 1;
    return `je-${counter.toString(36)}-${Date.now().toString(36)}`;
}

export function isLineLocked(line: EditLine): boolean {
    return line.status !== 'Uncleared';
}

export function emptyLine(): EditLine {
    return {
        key: nextLineKey(),
        serverId: null,
        status: 'Uncleared',
        accountId: null,
        side: 'debit',
        amount: '',
        description: '',
    };
}

function formatMagnitude(minor: number, scale: number): string {
    const absMinor = Math.abs(minor);
    const divisor = 10 ** scale;
    const major = Math.floor(absMinor / divisor);
    const remainder = absMinor - major * divisor;
    if (scale === 0) return major.toString();
    return `${major.toString()}.${remainder.toString().padStart(scale, '0')}`;
}

export type LoadedLine = {
    id: JournalLineId;
    accountId: AccountId;
    amount: number;
    description: string | null;
    reconciliationStatus: WireReconciliationStatus;
};

export function toEditLines(lines: readonly LoadedLine[], scale: number): EditLine[] {
    return lines.map(line => ({
        key: line.id,
        serverId: line.id,
        status: line.reconciliationStatus,
        accountId: line.accountId,
        side: line.amount >= 0 ? 'debit' : 'credit',
        amount: formatMagnitude(line.amount, scale),
        description: line.description ?? '',
    }));
}

export type TotalsState = {
    debitMinor: number;
    creditMinor: number;
    balanced: boolean;
};

/** Sum the magnitudes per side, skipping lines that fail to parse — the form-
 *  level validation surfaces those when the user submits. */
export function computeTotals(lines: readonly EditLine[], scale: number): TotalsState {
    let debit = 0;
    let credit = 0;
    for (const line of lines) {
        if (line.accountId === null) continue;
        const parsed = parseMoney(line.amount, scale);
        if (!parsed.ok) continue;
        const magnitude = Math.abs(parsed.minor);
        if (line.side === 'debit') debit += magnitude;
        else credit += magnitude;
    }
    return { debitMinor: debit, creditMinor: credit, balanced: debit === credit && debit > 0 };
}

export type BuildReplaceContext = {
    date: string;
    description: string;
    counterpartyId: components['schemas']['CounterpartyId'] | null;
    lines: readonly EditLine[];
    scale: number;
};

export function buildReplaceRequest(ctx: BuildReplaceContext): BuildReplaceResult {
    const errors: FieldErrors = {};
    const wireLines: WireReplaceLine[] = [];

    if (ctx.date.trim() === '') {
        errors.date = ['Required'];
    }

    let nonEmptyCount = 0;
    let totalSigned = 0;

    ctx.lines.forEach((line, i) => {
        const empty =
            line.accountId === null &&
            line.amount.trim() === '' &&
            line.description.trim() === '' &&
            line.serverId === null;
        if (empty) return;
        nonEmptyCount += 1;

        const accountKey = `lines[${i.toString()}].accountId`;
        const amountKey = `lines[${i.toString()}].amount`;

        if (line.accountId === null) {
            errors[accountKey] = ['Select an account.'];
        }
        const parsed = parseMoney(line.amount, ctx.scale);
        if (!parsed.ok) {
            errors[amountKey] = [parsed.error];
            return;
        }
        const magnitude = Math.abs(parsed.minor);
        if (magnitude === 0) {
            errors[amountKey] = ['Amount must be non-zero.'];
            return;
        }

        if (line.accountId === null) return;

        const signed = line.side === 'debit' ? magnitude : -magnitude;
        totalSigned += signed;
        const trimmedDescription = line.description.trim();
        wireLines.push({
            id: line.serverId,
            accountId: line.accountId,
            amount: signed,
            description: trimmedDescription.length === 0 ? null : trimmedDescription,
            // Frozen lines must round-trip their current status; new/uncleared lines stay null.
            reconciliationStatus: line.serverId === null ? null : line.status,
        });
    });

    if (nonEmptyCount < 2) {
        errors.lines = ['At least two lines are required.'];
    }

    if (Object.keys(errors).length > 0) {
        return { ok: false, fieldErrors: errors };
    }

    if (totalSigned !== 0) {
        return {
            ok: false,
            fieldErrors: errors,
            topError: 'Debit and Credit totals must balance to zero.',
        };
    }

    const trimmedDescription = ctx.description.trim();

    return {
        ok: true,
        request: {
            date: ctx.date,
            description: trimmedDescription.length === 0 ? null : trimmedDescription,
            counterpartyId: ctx.counterpartyId,
            lines: wireLines,
        },
    };
}
