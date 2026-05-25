import { createFileRoute } from '@tanstack/react-router';
import { JournalDetail } from '../screens/JournalDetail';
import { asJournalEntryId } from '../lib/domain';

export const Route = createFileRoute('/journal/$id')({
    component: function JournalDetailRoute() {
        const { id } = Route.useParams();
        return <JournalDetail id={asJournalEntryId(id)} />;
    },
    staticData: { title: 'Journal entry' },
});
