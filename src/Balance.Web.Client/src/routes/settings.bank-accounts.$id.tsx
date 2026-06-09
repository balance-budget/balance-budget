import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { BankAccountDetail } from '../screens/BankAccounts';
import { asBankAccountId } from '../lib/domain';

export const Route = createFileRoute('/settings/bank-accounts/$id')({
    component: function BankAccountDetailRoute() {
        const { id } = Route.useParams();
        return <BankAccountDetail id={asBankAccountId(id)} />;
    },
    staticData: { title: msg`Bank account` },
});
