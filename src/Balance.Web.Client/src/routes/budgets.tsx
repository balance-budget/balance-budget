import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/budgets')({
    component: () => <Empty title="Budgets" />,
});
