import { createFileRoute } from '@tanstack/react-router';
import { AccountDetail } from '../screens/AccountDetail';
import { asAccountId } from '../lib/domain';

export const Route = createFileRoute('/accounts/$id')({
    component: function AccountDetailRoute() {
        const { id } = Route.useParams();
        return <AccountDetail id={asAccountId(id)} />;
    },
    staticData: { title: 'Account' },
});
