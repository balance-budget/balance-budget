import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/accounts')({
    component: () => <Empty title="Accounts" />,
});
