import { asBudgetId, type BudgetSummary } from '../lib/domain';
import {
    ACCOUNT_BILLS,
    ACCOUNT_DINING,
    ACCOUNT_GROCERIES,
    ACCOUNT_RENT,
    ACCOUNT_TRANSPORT,
} from './accounts';

export const BUDGETS: BudgetSummary[] = [
    {
        id: asBudgetId('019709a8-4f60-7c11-b6a1-3c2c1a3da001'),
        name: 'Groceries',
        expenseAccountId: ACCOUNT_GROCERIES,
        spentMinor: 18_240, // €182.40
        limitMinor: 30_000, // €300.00
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-food)',
    },
    {
        id: asBudgetId('019709a8-4f60-7c11-b6a1-3c2c1a3da002'),
        name: 'Dining',
        expenseAccountId: ACCOUNT_DINING,
        spentMinor: 9_320, // €93.20
        limitMinor: 10_000,
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-food)',
    },
    {
        id: asBudgetId('019709a8-4f60-7c11-b6a1-3c2c1a3da003'),
        name: 'Transport',
        expenseAccountId: ACCOUNT_TRANSPORT,
        spentMinor: 6_500,
        limitMinor: 15_000,
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-transport)',
    },
    {
        id: asBudgetId('019709a8-4f60-7c11-b6a1-3c2c1a3da004'),
        name: 'Bills',
        expenseAccountId: ACCOUNT_BILLS,
        spentMinor: 21_840,
        limitMinor: 20_000,
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-bills)',
    },
    {
        id: asBudgetId('019709a8-4f60-7c11-b6a1-3c2c1a3da005'),
        name: 'Rent',
        expenseAccountId: ACCOUNT_RENT,
        spentMinor: 120_000,
        limitMinor: 120_000,
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-housing)',
    },
];
