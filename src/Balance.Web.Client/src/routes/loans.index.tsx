import { createFileRoute } from '@tanstack/react-router';
import { Loans } from '../screens/Loans';

export const Route = createFileRoute('/loans/')({
    component: Loans,
    staticData: { title: 'Loans' },
});
