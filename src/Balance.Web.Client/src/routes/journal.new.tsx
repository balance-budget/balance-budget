import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { JournalNew } from '../screens/JournalNew';

export const Route = createFileRoute('/journal/new')({
    component: JournalNew,
    staticData: { title: msg`New journal entry` },
});
