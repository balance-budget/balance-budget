import { createFileRoute } from '@tanstack/react-router';
import { BankTransactionCategorize } from '../screens/BankTransactionCategorize';
import { asBankTransactionId, asLoanId, type LoanId } from '../lib/domain';

type CategorizeSearch = {
    /** Loan-aware mode (ADR-0025): pre-fill the engine's payment proposal for this loan. */
    loan?: LoanId;
};

export const Route = createFileRoute('/bank-transactions/$id/categorize')({
    component: function CategorizeRoute() {
        const { id } = Route.useParams();
        const { loan } = Route.useSearch();
        return <BankTransactionCategorize id={asBankTransactionId(id)} loanId={loan ?? null} />;
    },
    staticData: { title: 'Categorise bank transaction' },
    validateSearch: (raw: Record<string, unknown>): CategorizeSearch => ({
        loan: typeof raw.loan === 'string' && raw.loan !== '' ? asLoanId(raw.loan) : undefined,
    }),
});
