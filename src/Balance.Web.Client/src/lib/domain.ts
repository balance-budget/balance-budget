/*
 * Frontend view-model types. Per ADR-0004, IDs round-trip as GUID-shaped
 * strings on the wire — branded here for parameter-swap safety without
 * pulling in a runtime newtype library.
 *
 * Resource-specific view-models live alongside the fetcher in `src/api/*.ts`.
 * This module owns the cross-cutting brand types and the trend-chart
 * view-model consumed by TrendChart.
 */

declare const __brand: unique symbol;
type Brand<T, B> = T & { readonly [__brand]: B };

export type AccountId = Brand<string, 'AccountId'>;
export type JournalEntryId = Brand<string, 'JournalEntryId'>;
export type JournalLineId = Brand<string, 'JournalLineId'>;
export type CounterpartyId = Brand<string, 'CounterpartyId'>;
export type BankTransactionId = Brand<string, 'BankTransactionId'>;
export type BankAccountId = Brand<string, 'BankAccountId'>;

export const asAccountId = (s: string) => s as AccountId;
export const asJournalEntryId = (s: string) => s as JournalEntryId;
export const asJournalLineId = (s: string) => s as JournalLineId;
export const asCounterpartyId = (s: string) => s as CounterpartyId;
export const asBankTransactionId = (s: string) => s as BankTransactionId;
export const asBankAccountId = (s: string) => s as BankAccountId;

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';

// "Ledger" = the user's real-money accounts (Asset + Liability). "Category" =
// where money comes from / goes (Income + Expense). Equity is bookkeeping
// plumbing and isn't navigable in the UI. See Sidebar / Dashboard for the
// rendering split.
export const isLedgerAccount = (a: { type: AccountType }): boolean =>
    a.type === 'Asset' || a.type === 'Liability';

export const isCategoryAccount = (a: { type: AccountType }): boolean =>
    a.type === 'Income' || a.type === 'Expense';

export type TrendPoint = { date: string; balanceMinor: number };

export type AccountTrend = {
    accountId: AccountId;
    name: string;
    accentColor: string;
    points: TrendPoint[];
};
