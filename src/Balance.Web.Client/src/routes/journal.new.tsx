import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { asAccountId } from '../lib/domain';
import { JournalNew } from '../screens/JournalNew';

type JournalNewSearch = {
    /** Account to preselect as the first Simple leg; absent means none. */
    accountId?: string;
};

export const Route = createFileRoute('/journal/new')({
    component: function JournalNewRoute() {
        const { accountId } = Route.useSearch();
        return (
            <JournalNew
                prefillAccountId={accountId === undefined ? null : asAccountId(accountId)}
            />
        );
    },
    staticData: { title: msg`New journal entry` },
    validateSearch: (raw: Record<string, unknown>): JournalNewSearch =>
        typeof raw.accountId === 'string' && raw.accountId !== ''
            ? { accountId: raw.accountId }
            : {},
});
