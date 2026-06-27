import { useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import { useLoans, type Loan } from '../api/loans';
import { Amount } from '../components/Amount';
import { Empty } from '../components/Empty';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Cell, Column, Row, Table, TableBody, TableHeader } from '../components/ui/Table';
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
            <LoanTable loans={active} label={t`Active loans`} />
            {ended.length > 0 && (
                <>
                    <div className="text-xs font-medium text-fg-3 uppercase tracking-wide mt-4 mb-1">
                        <Trans>Ended</Trans>
                    </div>
                    <LoanTable loans={ended} label={t`Ended loans`} />
                </>
            )}
        </div>
    );
}

function LoanTable({ loans, label }: { loans: Loan[]; label: string }) {
    const navigate = useNavigate();
    return (
        <Table
            aria-label={label}
            onRowAction={key => {
                void navigate({ to: '/loans/$id', params: { id: String(key) } });
            }}
        >
            <TableHeader>
                <Column isRowHeader>
                    <Trans>Loan</Trans>
                </Column>
                <Column className="text-right">
                    <Trans>Outstanding</Trans>
                </Column>
                <Column className="text-right">
                    <Trans>Monthly payment</Trans>
                </Column>
                <Column className="text-right">
                    <Trans>Weighted rate</Trans>
                </Column>
            </TableHeader>
            <TableBody items={loans}>{loan => <LoanRow loan={loan} />}</TableBody>
        </Table>
    );
}

function LoanRow({ loan }: { loan: Loan }) {
    return (
        <Row id={loan.id} className="cursor-pointer">
            <Cell className="py-3 pr-3 align-middle">
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
            </Cell>
            <Cell className="py-3 pr-3 text-right align-middle">
                <Amount
                    minor={loan.outstandingBalance}
                    currencyCode={loan.currencyCode}
                    size="inline"
                />
            </Cell>
            <Cell className="py-3 pr-3 text-right align-middle">
                <Amount
                    minor={loan.currentPayment}
                    currencyCode={loan.currencyCode}
                    size="inline"
                />
            </Cell>
            <Cell className="py-3 pr-3 text-right align-middle">
                <span className="text-sm font-medium tabular-nums">
                    {loan.weightedAnnualRatePercent === null
                        ? '—'
                        : `${loan.weightedAnnualRatePercent.toFixed(2)}%`}
                </span>
            </Cell>
        </Row>
    );
}
