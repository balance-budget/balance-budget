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

    const fromLegs: ProjectionLeg[] = isSimplifiable
        ? sortLegs(creditAccounts)
        : [];
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
    legs.sort((a, b) => (a.accountName < b.accountName ? -1 : a.accountName > b.accountName ? 1 : 0));
    return legs;
}
