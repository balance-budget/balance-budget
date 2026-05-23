import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/reports')({
    component: () => <Empty title="Reports" />,
    staticData: { title: 'Reports' },
});
