import { createFileRoute } from '@tanstack/react-router';
import { BankAccounts } from '../screens/BankAccounts';

export const Route = createFileRoute('/settings/bank-accounts/')({
    component: BankAccounts,
    staticData: { title: 'Bank accounts' },
});
