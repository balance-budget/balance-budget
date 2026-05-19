import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/subscriptions')({
    component: () => <Empty title="Subscriptions" />,
});
