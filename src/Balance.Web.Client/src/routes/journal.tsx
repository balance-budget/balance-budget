import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/journal')({
    component: () => <Empty title="Journal entries" />,
    staticData: { title: 'Journal entries' },
});
