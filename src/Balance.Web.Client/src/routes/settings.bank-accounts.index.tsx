import { createFileRoute, useNavigate } from '@tanstack/react-router';
import {
    BANK_ACCOUNT_OWNER_FILTERS,
    BankAccounts,
    type BankAccountOwnerFilter,
} from '../screens/BankAccounts';

type Search = { owner: BankAccountOwnerFilter };

function isOwner(value: unknown): value is BankAccountOwnerFilter {
    return (
        typeof value === 'string' &&
        (BANK_ACCOUNT_OWNER_FILTERS as readonly string[]).includes(value)
    );
}

export const Route = createFileRoute('/settings/bank-accounts/')({
    component: function BankAccountsRoute() {
        const { owner } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <BankAccounts
                owner={owner}
                onOwnerChange={o => {
                    void navigate({ search: { owner: o } });
                }}
            />
        );
    },
    staticData: { title: 'Bank accounts' },
    validateSearch: (raw: Record<string, unknown>): Search => ({
        owner: isOwner(raw.owner) ? raw.owner : 'mine',
    }),
});
