import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';

export const Route = createFileRoute('/piggy-banks')({
    component: () => <Empty title="Piggy banks" />,
});
