import { createFileRoute } from '@tanstack/react-router';
import { Accounts } from '../screens/Accounts';

export const Route = createFileRoute('/accounts/')({
    component: Accounts,
    staticData: { title: 'Accounts' },
});
