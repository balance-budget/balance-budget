import { msg } from '@lingui/core/macro';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { BANK_ACCOUNT_OWNER_FILTERS, type BankAccountOwnerFilter } from '../api/bankAccounts';
import { BankAccounts } from '../screens/BankAccounts';
import { parsePage, parseQ } from '../lib/routeSearch';

type Search = { owner: BankAccountOwnerFilter; page: number; q: string };

function isOwner(value: unknown): value is BankAccountOwnerFilter {
    return (
        typeof value === 'string' &&
        (BANK_ACCOUNT_OWNER_FILTERS as readonly string[]).includes(value)
    );
}

export const Route = createFileRoute('/settings/bank-accounts/')({
    component: function BankAccountsRoute() {
        const { owner, page, q } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <BankAccounts
                owner={owner}
                page={page}
                q={q}
                onOwnerChange={o => {
                    void navigate({ search: prev => ({ ...prev, owner: o, page: 1 }) });
                }}
                onPageChange={p => {
                    void navigate({ search: prev => ({ ...prev, page: p }) });
                }}
                onSearchChange={value => {
                    void navigate({ search: prev => ({ ...prev, page: 1, q: value }) });
                }}
            />
        );
    },
    staticData: { title: msg`Bank accounts` },
    validateSearch: (raw: Record<string, unknown>): Search => ({
        owner: isOwner(raw.owner) ? raw.owner : 'Mine',
        page: parsePage(raw.page),
        q: parseQ(raw.q),
    }),
});
