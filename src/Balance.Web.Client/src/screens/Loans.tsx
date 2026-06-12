import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import { useLoans, type Loan } from '../api/loans';
import { Amount } from '../components/Amount';
import { Empty } from '../components/Empty';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { LoanFormModal } from './LoanForm';

/**
 * The Loans section: the debt position at a glance — outstanding balance,
 * current payment, and the balance-weighted rate per loan (ADR-0025).
 */
export function Loans() {
    const [creating, setCreating] = useState(false);
    const loans = useLoans();

    return (
        <>
            <Panel>
                <SectionHead
                    subtitle={
                        <Trans>
                            Mortgages and other loans, tracked part by part over the ledger.
                        </Trans>
                    }
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg bg-brand-primary text-white text-sm font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            <Trans>New loan</Trans>
                        </button>
                    }
                />
                <LoanList loans={loans} />
            </Panel>

            {creating && (
                <LoanFormModal
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
        </>
    );
}

function LoanList({ loans }: { loans: ReturnType<typeof useLoans> }) {
    const { t } = useLingui();

    if (loans.isPending) return <Skeleton className="h-24" />;

    if (loans.isError) return <ErrorState message={t`Couldn't load loans.`} />;

    const items = loans.data;
    if (items.length === 0) {
        return (
            <Empty
                title={t`No loans yet`}
                hint={t`Define your mortgage or another loan to split monthly payments into interest and principal, and to see where the debt is heading.`}
            />
        );
    }

    const active = items.filter(l => !l.isEnded);
    const ended = items.filter(l => l.isEnded);

    return (
        <div className="flex flex-col gap-2">
            {active.map(loan => (
                <LoanRow key={loan.id} loan={loan} />
            ))}
            {ended.length > 0 && (
                <>
                    <div className="text-xs font-medium text-fg-3 uppercase tracking-wide mt-4 mb-1">
                        <Trans>Ended</Trans>
                    </div>
                    {ended.map(loan => (
                        <LoanRow key={loan.id} loan={loan} />
                    ))}
                </>
            )}
        </div>
    );
}

function LoanRow({ loan }: { loan: Loan }) {
    const { t } = useLingui();
    return (
        <Link
            to="/loans/$id"
            params={{ id: loan.id }}
            className="grid grid-cols-[minmax(0,1.4fr)_repeat(3,minmax(0,1fr))] items-center gap-3 px-3 py-3 rounded-lg border border-border-soft hover:border-border-strong transition-colors"
        >
            <div className="min-w-0">
                <div className="flex items-center gap-2">
                    <Icon name="landmark" size={16} strokeWidth={1.75} className="text-fg-3" />
                    <span className="text-sm font-medium text-fg-1 truncate">{loan.name}</span>
                    {loan.isEnded && (
                        <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-surface-2 text-fg-3">
                            <Trans>Ended</Trans>
                        </span>
                    )}
                </div>
                <div className="text-xs text-fg-3 mt-0.5 truncate">
                    {loan.lenderName} ·{' '}
                    <Plural value={loan.partCount} one="# part" other="# parts" />
                </div>
            </div>
            <Stat label={t`Outstanding`}>
                <Amount
                    minor={loan.outstandingBalance}
                    currencyCode={loan.currencyCode}
                    size="inline"
                />
            </Stat>
            <Stat label={t`Monthly payment`}>
                <Amount
                    minor={loan.currentPayment}
                    currencyCode={loan.currencyCode}
                    size="inline"
                />
            </Stat>
            <Stat label={t`Weighted rate`}>
                <span className="text-sm font-medium tabular-nums">
                    {loan.weightedAnnualRatePercent === null
                        ? '—'
                        : `${loan.weightedAnnualRatePercent.toFixed(2)}%`}
                </span>
            </Stat>
        </Link>
    );
}

function Stat({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div className="flex flex-col items-end gap-0.5">
            <span className="text-[11px] text-fg-3">{label}</span>
            {children}
        </div>
    );
}
