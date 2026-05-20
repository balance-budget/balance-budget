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

export const asAccountId = (s: string) => s as AccountId;
export const asJournalEntryId = (s: string) => s as JournalEntryId;
export const asJournalLineId = (s: string) => s as JournalLineId;

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';

export type TrendPoint = { date: string; balanceMinor: number };

export type AccountTrend = {
    accountId: AccountId;
    name: string;
    accentColor: string;
    points: TrendPoint[];
};
