import { asAccountId, type AccountSummary } from '../lib/domain';

export const ACCOUNT_CHECKING = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e10');
export const ACCOUNT_SAVINGS = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e11');
export const ACCOUNT_CREDIT = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e12');
export const ACCOUNT_CASH = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e13');

export const ACCOUNT_GROCERIES = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e20');
export const ACCOUNT_RENT = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e21');
export const ACCOUNT_TRANSPORT = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e22');
export const ACCOUNT_BILLS = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e23');
export const ACCOUNT_DINING = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e24');
export const ACCOUNT_SALARY = asAccountId('019709a8-4f60-7c11-b6a1-3c2c1a3d9e30');

export const ACCOUNTS: AccountSummary[] = [
    {
        id: ACCOUNT_CHECKING,
        name: 'ABN AMRO Checking',
        type: 'Asset',
        balanceMinor: 320_450, // €3,204.50
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-transport)',
        iconName: 'wallet',
        bankAccountNumber: '· 4821',
    },
    {
        id: ACCOUNT_SAVINGS,
        name: 'Savings',
        type: 'Asset',
        balanceMinor: 841_230, // €8,412.30
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-savings)',
        iconName: 'piggy-bank',
        bankAccountNumber: '· 9930',
    },
    {
        id: ACCOUNT_CREDIT,
        name: 'Visa Credit Card',
        type: 'Liability',
        balanceMinor: -18_472, // owed: -€184.72
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-shopping)',
        iconName: 'credit-card',
        bankAccountNumber: '· 1102',
    },
    {
        id: ACCOUNT_CASH,
        name: 'Cash',
        type: 'Asset',
        balanceMinor: 14_200, // €142.00
        currencyCode: 'EUR',
        accentColor: 'var(--color-cat-bills)',
        iconName: 'banknote',
        bankAccountNumber: null,
    },
];
