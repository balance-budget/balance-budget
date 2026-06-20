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
export type LoanId = Brand<string, 'LoanId'>;
export type LoanPartId = Brand<string, 'LoanPartId'>;
export type LoanPartRatePeriodId = Brand<string, 'LoanPartRatePeriodId'>;
export type JournalEntryTemplateId = Brand<string, 'JournalEntryTemplateId'>;

export const asAccountId = (s: string) => s as AccountId;
export const asJournalEntryId = (s: string) => s as JournalEntryId;
export const asJournalLineId = (s: string) => s as JournalLineId;
export const asCounterpartyId = (s: string) => s as CounterpartyId;
export const asBankTransactionId = (s: string) => s as BankTransactionId;
export const asBankAccountId = (s: string) => s as BankAccountId;
export const asLoanId = (s: string) => s as LoanId;
export const asLoanPartId = (s: string) => s as LoanPartId;
export const asLoanPartRatePeriodId = (s: string) => s as LoanPartRatePeriodId;
export const asJournalEntryTemplateId = (s: string) => s as JournalEntryTemplateId;

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';

// "Ledger" = the user's real-money accounts (Asset + Liability). "Category" =
// where money comes from / goes (Income + Expense). Equity is bookkeeping
// plumbing and isn't navigable in the UI. See Sidebar / Dashboard for the
// rendering split.
export const isLedgerAccount = (a: { type: AccountType }): boolean =>
    a.type === 'Asset' || a.type === 'Liability';

export const isCategoryAccount = (a: { type: AccountType }): boolean =>
    a.type === 'Income' || a.type === 'Expense';

/** Canonical display order for grouping accounts by type (balance-sheet first, then P&L). */
export const ACCOUNT_TYPE_ORDER: AccountType[] = [
    'Asset',
    'Liability',
    'Income',
    'Expense',
    'Equity',
];

/** Plural display labels for each AccountType (optgroup / section headers). */
export const ACCOUNT_TYPE_LABEL: Record<AccountType, string> = {
    Asset: 'Assets',
    Liability: 'Liabilities',
    Income: 'Income',
    Expense: 'Expenses',
    Equity: 'Equity',
};

/** When the holder expects to draw on an account's money — drives the dashboard's tiered balance
 *  charts (ADR-0030). Orthogonal to liquidity; meaningful only on Asset/Liability accounts. */
export type Horizon = 'ShortTerm' | 'MediumTerm' | 'LongTerm';

/** Short → long ordering for the Horizon select and chart tiers. */
export const HORIZON_ORDER: Horizon[] = ['ShortTerm', 'MediumTerm', 'LongTerm'];

export type TrendPoint = { date: string; balanceMinor: number };

export type AccountTrend = {
    accountId: AccountId;
    name: string;
    accentColor: string;
    points: TrendPoint[];
};
