/*
 * Frontend types mirroring the domain in CONTEXT.md and the ADRs. The real
 * API does not exist yet — these are the shapes we expect a /api/accounts
 * or /api/journal-entries response to take once it does. Demo data is
 * shaped to satisfy these so screens stay correct when the endpoint lands.
 *
 * Per ADR-0004, IDs are GUID-shaped strings on the wire. We brand them
 * here for parameter-swap safety without pulling in a runtime newtype lib.
 */

declare const __brand: unique symbol;
type Brand<T, B> = T & { readonly [__brand]: B };

export type AccountId = Brand<string, 'AccountId'>;
export type JournalEntryId = Brand<string, 'JournalEntryId'>;
export type CounterpartyId = Brand<string, 'CounterpartyId'>;
export type BudgetId = Brand<string, 'BudgetId'>;
export type SubscriptionId = Brand<string, 'SubscriptionId'>;
export type BankTransactionId = Brand<string, 'BankTransactionId'>;

/** Strip the brand for places that only have a raw string at hand (e.g. seed data). */
export const asAccountId = (s: string) => s as AccountId;
export const asJournalEntryId = (s: string) => s as JournalEntryId;
export const asBudgetId = (s: string) => s as BudgetId;
export const asSubscriptionId = (s: string) => s as SubscriptionId;

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';

export type AccountSummary = {
    id: AccountId;
    name: string;
    type: AccountType;
    balanceMinor: number;
    currencyCode: string;
    /** Visual hint — would live in a per-user UI preferences table, not on the entity itself. */
    accentColor: string;
    iconName: string;
    /** Last four of IBAN / account number, when this Account is linked to a BankAccount. */
    bankAccountNumber: string | null;
};

/**
 * What a /api/journal-entries list row will likely return: an entry collapsed
 * to its primary user-side leg, with the offsetting expense/income account
 * surfaced alongside for badge display.
 */
export type JournalEntrySummary = {
    id: JournalEntryId;
    date: string;
    counterpartyName: string | null;
    description: string | null;
    /** Net effect on the focal account — negative = money out. */
    amountMinor: number;
    currencyCode: string;
    accountId: AccountId;
    categoryAccountId: AccountId | null;
    categoryAccountName: string | null;
    /** Tint key — matches the offsetting account's accentColor. */
    accentColor: string;
    iconName: string;
};

export type BudgetSummary = {
    id: BudgetId;
    name: string;
    expenseAccountId: AccountId;
    spentMinor: number;
    limitMinor: number;
    currencyCode: string;
    accentColor: string;
};

export type SubscriptionSummary = {
    id: SubscriptionId;
    counterpartyName: string;
    amountMinor: number;
    currencyCode: string;
    cadence: 'monthly' | 'yearly';
    nextDate: string;
    iconName: string;
};

export type TrendPoint = { day: number; balanceMinor: number };

export type AccountTrend = {
    accountId: AccountId;
    name: string;
    accentColor: string;
    points: TrendPoint[];
};
