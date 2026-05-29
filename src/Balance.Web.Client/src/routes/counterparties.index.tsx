import { createFileRoute } from '@tanstack/react-router';
import { Counterparties } from '../screens/Counterparties';

export const Route = createFileRoute('/counterparties/')({
    component: Counterparties,
    staticData: { title: 'Counterparties' },
});
