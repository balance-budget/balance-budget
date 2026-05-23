import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/bank-imports')({
    component: () => <Empty title="Bank imports" />,
    staticData: { title: 'Bank imports' },
});
