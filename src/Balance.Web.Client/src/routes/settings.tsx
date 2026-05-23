import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/settings')({
    component: () => <Empty title="Settings" />,
    staticData: { title: 'Settings' },
});
