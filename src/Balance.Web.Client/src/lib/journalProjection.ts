/*
 * Pure UI-shaped projection of a JournalEntry's lines, per ADR-0012.
 *
 * Mirrors the same rules the C# implementation used before issue #82:
 *
 *   - netWorthChange = Σ amount(Asset lines) + Σ amount(Liability lines)
 *     (a liability debit reduces liability balance, so the formula collapses
 *      ΔAssets − ΔLiabilities to the signed sum above).
 *   - isTransfer = netWorthChange == 0.
 *   - grossMagnitude = Σ positive amounts (the debits).
 *   - isSimplifiable = exactly one distinct debit account or exactly one
 *     distinct credit account (1-to-N or N-to-1).
 *   - When simplifiable, fromLegs are the credit-side accounts and toLegs are
 *     the debit-side accounts, each deduplicated and ordered by accountName.
 *
 * The journal overview is the sole consumer; the SPA already has
 * AccountType cached via useAccounts(), so the projection runs at render time
 * instead of being baked into the API response.
 */

import type { Account } from '../api/accounts';
import type { JournalEntry } from '../api/journalEntries';
import type { AccountId, AccountType, JournalLineId } from './domain';
import type { Money } from './money';

export type ProjectionLine = {
    id: JournalLineId;
    accountId: AccountId;
    accountName: string;
    accountType: AccountType;
    amount: number;
};

export type ProjectionLeg = {
    accountId: AccountId;
    accountName: string;
};

export type JournalProjection = {
    isTransfer: boolean;
    netWorthChange: Money;
    grossMagnitude: Money;
    isSimplifiable: boolean;
    fromLegs: ProjectionLeg[];
    toLegs: ProjectionLeg[];
};

export function projectJournalEntry(
    lines: readonly ProjectionLine[],
    currencyCode: string,
): JournalProjection {
    let assetSum = 0;
    let liabilitySum = 0;
    let grossMagnitude = 0;

    const debitAccounts = new Map<AccountId, string>();
    const creditAccounts = new Map<AccountId, string>();

    for (const line of lines) {
        if (line.accountType === 'Asset') {
            assetSum += line.amount;
        } else if (line.accountType === 'Liability') {
            liabilitySum += line.amount;
        }

        if (line.amount > 0) {
            grossMagnitude += line.amount;
            if (!debitAccounts.has(line.accountId)) {
                debitAccounts.set(line.accountId, line.accountName);
            }
        } else if (line.amount < 0) {
            if (!creditAccounts.has(line.accountId)) {
                creditAccounts.set(line.accountId, line.accountName);
            }
        }
    }

    const netWorthChange = assetSum + liabilitySum;
    const isTransfer = netWorthChange === 0;
    const isSimplifiable = debitAccounts.size === 1 || creditAccounts.size === 1;

    const fromLegs: ProjectionLeg[] = isSimplifiable ? sortLegs(creditAccounts) : [];
    const toLegs: ProjectionLeg[] = isSimplifiable ? sortLegs(debitAccounts) : [];

    return {
        isTransfer,
        netWorthChange: { amount: netWorthChange, currencyCode },
        grossMagnitude: { amount: grossMagnitude, currencyCode },
        isSimplifiable,
        fromLegs,
        toLegs,
    };
}

function sortLegs(accounts: ReadonlyMap<AccountId, string>): ProjectionLeg[] {
    const legs: ProjectionLeg[] = [];
    for (const [accountId, accountName] of accounts) {
        legs.push({ accountId, accountName });
    }
    legs.sort((a, b) =>
        a.accountName < b.accountName ? -1 : a.accountName > b.accountName ? 1 : 0,
    );
    return legs;
}

/**
 * Adapter that projects a loaded `JournalEntry` for UI rendering. Currency is
 * taken off the first line's account — balanced entries are single-currency
 * (enforced by the backend validator) — and falls back to "XXX" so an empty
 * entry still produces a Money payload.
 */
export function projectEntry(
    entry: JournalEntry,
    accountById: ReadonlyMap<AccountId, Account>,
): JournalProjection {
    const firstLineAccount = entry.lines[0] ? accountById.get(entry.lines[0].accountId) : undefined;
    const currencyCode = firstLineAccount?.currencyCode ?? 'XXX';
    const projectionLines: ProjectionLine[] = entry.lines.map(line => ({
        id: line.id,
        accountId: line.accountId,
        accountName: line.accountName,
        accountType: accountById.get(line.accountId)?.type ?? 'Asset',
        amount: line.amount,
    }));
    return projectJournalEntry(projectionLines, currencyCode);
}

/**
 * Renders the From/To label for a simplifiable projection: "—" for an empty
 * leg, the single name when there's one account, or "{first} +{n}" when
 * multiple accounts collapse onto one side.
 */
export function formatLegLabel(legs: readonly ProjectionLeg[]): string {
    const first = legs[0];
    if (!first) return '—';
    if (legs.length === 1) return first.accountName;
    return `${first.accountName} +${legs.length - 1}`;
}
