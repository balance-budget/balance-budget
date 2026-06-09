import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { asLoanId } from '../lib/domain';
import { LoanDetail } from '../screens/LoanDetail';

export const Route = createFileRoute('/loans/$id')({
    component: function LoanDetailRoute() {
        const { id } = Route.useParams();
        return <LoanDetail id={asLoanId(id)} />;
    },
    staticData: { title: msg`Loan` },
});
