import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Outlook } from '../screens/Outlook';

export const Route = createFileRoute('/outlook')({
    component: Outlook,
    staticData: { title: msg`Outlook` },
});
