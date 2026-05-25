import { createFileRoute } from '@tanstack/react-router';
import { BankTransactionCategorize } from '../screens/BankTransactionCategorize';
import { asBankTransactionId } from '../lib/domain';

export const Route = createFileRoute('/bank-transactions/$id/categorize')({
    component: function CategorizeRoute() {
        const { id } = Route.useParams();
        return <BankTransactionCategorize id={asBankTransactionId(id)} />;
    },
    staticData: { title: 'Categorise bank transaction' },
});
