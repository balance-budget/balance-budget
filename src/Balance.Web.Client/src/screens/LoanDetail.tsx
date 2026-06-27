import { useMemo, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { t as coreT } from '@lingui/core/macro';
import { Trans, useLingui } from '@lingui/react/macro';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useDeleteLoan,
    useDeleteLoanPart,
    useDeleteRatePeriod,
    useLoan,
    useLoanProjection,
    type LoanDetail as LoanDetailModel,
    type LoanPart,
    type LoanRatePeriod,
} from '../api/loans';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { LoanChart } from '../components/LoanChart';
import { Panel, SectionHead } from '../components/Panel';
import { usePageHeader } from '../components/PageHeader';
import { Skeleton } from '../components/Skeleton';
import { Button } from '../components/ui/Button';
import { useToast } from '../components/ui/Toast';
import { formatTableDate, todayIso } from '../lib/dates';
import type { LoanId, LoanPartId } from '../lib/domain';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import {
    AddLoanPartModal,
    AddRatePeriodModal,
    EditLoanModal,
    EditLoanPartModal,
    EditRatePeriodModal,
} from './LoanForm';
import { ScheduleTable } from './LoanScheduleTable';
import { Simulator } from './LoanSimulator';
import { buildScenario, initialSimulator, type SimulatorState } from './loanDetail.state';

export function LoanDetail({ id }: { id: LoanId }) {
    const { t } = useLingui();
    const loan = useLoan(id);
    // TopBar owns the title (the loan name) under a "Loans" breadcrumb; the
    // panel header keeps the lender/interest subtitle and the loan actions.
    usePageHeader({
        title: loan.data?.name,
        breadcrumb: [{ label: t`Loans`, to: '/loans' }],
    });

    if (loan.isPending) {
        return (
            <Panel>
                <Skeleton className="h-48" />
            </Panel>
        );
    }

    if (loan.isError) {
        return (
            <Panel>
                <ErrorState message={t`Couldn't load this loan.`} />
            </Panel>
        );
    }

    return <LoanDetailLoaded loan={loan.data} />;
}

function LoanDetailLoaded({ loan }: { loan: LoanDetailModel }) {
    const { t } = useLingui();
    const navigate = useNavigate();
    const toast = useToast();
    const deleteLoan = useDeleteLoan();
    const deletePart = useDeleteLoanPart();
    const deleteRate = useDeleteRatePeriod();
    const [simulator, setSimulator] = useState<SimulatorState>(() => initialSimulator(todayIso()));
    const [editingLoan, setEditingLoan] = useState(false);
    const [addingPart, setAddingPart] = useState(false);
    const [ratePart, setRatePart] = useState<LoanPart | null>(null);
    const [editingPart, setEditingPart] = useState<LoanPart | null>(null);
    const [deletingPart, setDeletingPart] = useState<LoanPart | null>(null);
    const [editingRate, setEditingRate] = useState<{
        partId: LoanPartId;
        rate: LoanRatePeriod;
    } | null>(null);
    const [deletingRate, setDeletingRate] = useState<{
        part: LoanPart;
        rate: LoanRatePeriod;
    } | null>(null);
    const [deleting, setDeleting] = useState(false);

    const catalog = useCurrencyCatalog();
    const scale = catalog.get(loan.currencyCode)?.minorUnitScale ?? 2;

    // The simulator is a server round-trip per (debounced) change — one engine
    // implementation only (ADR-0025).
    const scenario = useMemo(() => buildScenario(simulator, scale), [simulator, scale]);
    const debouncedScenario = useDebouncedValue(scenario, 400);
    const projection = useLoanProjection(loan.id, debouncedScenario);

    return (
        <div className="flex flex-col gap-4">
            <Panel>
                <SectionHead
                    subtitle={t`${loan.lenderName} · interest on ${loan.interestExpenseAccountName}`}
                    action={
                        <div className="flex items-center gap-2">
                            <Button
                                variant="secondary"
                                onPress={() => {
                                    setEditingLoan(true);
                                }}
                            >
                                <Icon name="pencil" size={14} strokeWidth={2} />
                                <Trans>Edit</Trans>
                            </Button>
                            <Button
                                variant="secondary"
                                onPress={() => {
                                    setAddingPart(true);
                                }}
                            >
                                <Icon name="plus" size={14} strokeWidth={2} />
                                <Trans>Add part</Trans>
                            </Button>
                            <Button
                                variant="danger"
                                onPress={() => {
                                    setDeleting(true);
                                }}
                            >
                                <Trans>Delete</Trans>
                            </Button>
                        </div>
                    }
                />
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <Kpi label={t`Outstanding`}>
                        <Amount
                            minor={loan.outstandingBalance}
                            currencyCode={loan.currencyCode}
                            size="medium"
                        />
                    </Kpi>
                    <Kpi label={t`Monthly payment`}>
                        <Amount
                            minor={loan.currentPayment}
                            currencyCode={loan.currencyCode}
                            size="medium"
                        />
                    </Kpi>
                    <Kpi label={t`Weighted rate`}>
                        <span className="text-xl font-medium tabular-nums">
                            {loan.weightedAnnualRatePercent === null
                                ? '—'
                                : `${loan.weightedAnnualRatePercent.toFixed(2)}%`}
                        </span>
                    </Kpi>
                    <Kpi label={t`Status`}>
                        <span className="text-xl font-medium">
                            {loan.isEnded ? <Trans>Ended</Trans> : <Trans>Active</Trans>}
                        </span>
                    </Kpi>
                </div>
                {loan.constructionDeposit && (
                    <div className="mt-3 rounded-lg bg-surface-2 px-3 py-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
                        <span className="inline-flex items-center gap-1.5 text-fg-2">
                            <Icon name="landmark" size={13} strokeWidth={2} />
                            <Trans>Construction deposit</Trans>
                        </span>
                        <span className="text-fg-3">
                            {loan.constructionDeposit.accountName} ·{' '}
                            <Amount
                                minor={loan.constructionDeposit.balance}
                                currencyCode={loan.currencyCode}
                                size="inline"
                            />{' '}
                            <Trans>
                                at {loan.constructionDeposit.annualRatePercent.toFixed(2)}%,
                                interest offsets the payment until it drains.
                            </Trans>
                        </span>
                    </div>
                )}
            </Panel>

            <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)] gap-4 items-start">
                <Panel>
                    <SectionHead
                        title={<Trans>Balance & payments</Trans>}
                        subtitle={
                            <Trans>
                                Posted actuals to the left of today, the engine&apos;s projection to
                                the right.
                            </Trans>
                        }
                    />
                    {projection.isPending ? (
                        <Skeleton className="h-[280px]" />
                    ) : projection.isError ? (
                        <ErrorState message={t`Couldn't compute the projection.`} />
                    ) : (
                        <LoanChart projection={projection.data} />
                    )}
                </Panel>

                <Panel>
                    <SectionHead
                        title={<Trans>What if…</Trans>}
                        subtitle={<Trans>Hypothetical extra repayments. Nothing is saved.</Trans>}
                    />
                    <Simulator
                        loan={loan}
                        simulator={simulator}
                        onChange={setSimulator}
                        totals={projection.data?.totals ?? null}
                    />
                </Panel>
            </div>

            <Panel>
                <SectionHead
                    title={<Trans>Parts</Trans>}
                    subtitle={<Trans>Parts, each with its own terms and rate history.</Trans>}
                />
                <PartsTable
                    loan={loan}
                    onAddRate={part => {
                        setRatePart(part);
                    }}
                    onEditPart={part => {
                        setEditingPart(part);
                    }}
                    onDeletePart={part => {
                        setDeletingPart(part);
                    }}
                    onEditRate={(partId, rate) => {
                        setEditingRate({ partId, rate });
                    }}
                    onDeleteRate={(part, rate) => {
                        setDeletingRate({ part, rate });
                    }}
                />
            </Panel>

            <Panel>
                <SectionHead
                    title={<Trans>Schedule</Trans>}
                    subtitle={
                        <Trans>
                            Past rows are posted actuals; future rows are the projection (the
                            what-if scenario when active).
                        </Trans>
                    }
                />
                {projection.data ? (
                    <ScheduleTable projection={projection.data} />
                ) : (
                    <Skeleton className="h-32" />
                )}
            </Panel>

            {editingLoan && (
                <EditLoanModal
                    loan={loan}
                    onClose={() => {
                        setEditingLoan(false);
                    }}
                />
            )}
            {addingPart && (
                <AddLoanPartModal
                    loan={loan}
                    onClose={() => {
                        setAddingPart(false);
                    }}
                />
            )}
            {ratePart && (
                <AddRatePeriodModal
                    loanId={loan.id}
                    part={ratePart}
                    onClose={() => {
                        setRatePart(null);
                    }}
                />
            )}
            {editingPart && (
                <EditLoanPartModal
                    loanId={loan.id}
                    part={editingPart}
                    onClose={() => {
                        setEditingPart(null);
                    }}
                />
            )}
            {editingRate && (
                <EditRatePeriodModal
                    loanId={loan.id}
                    partId={editingRate.partId}
                    rate={editingRate.rate}
                    onClose={() => {
                        setEditingRate(null);
                    }}
                />
            )}
            {deletingPart && (
                <ConfirmDialog
                    open
                    title={t`Delete ${deletingPart.label}?`}
                    message={t`The part definition and its rate history are removed. Its account and every posted entry stay untouched. It becomes an ordinary account again.`}
                    confirmLabel={t`Delete part`}
                    variant="destructive"
                    busy={deletePart.isPending}
                    onClose={() => {
                        setDeletingPart(null);
                    }}
                    onConfirm={() => {
                        void (async () => {
                            try {
                                await deletePart.mutateAsync({
                                    id: loan.id,
                                    partId: deletingPart.id,
                                });
                                toast.success(t`Part deleted.`);
                                setDeletingPart(null);
                            } catch {
                                toast.error(t`Couldn't delete the part.`);
                            }
                        })();
                    }}
                />
            )}
            {deletingRate && (
                <ConfirmDialog
                    open
                    title={t`Delete this rate period?`}
                    message={t`The ${deletingRate.rate.annualRatePercent.toFixed(2)}% rate effective ${formatTableDate(deletingRate.rate.effectiveDate)} is removed from ${deletingRate.part.label}. Projections recompute; posted entries are untouched.`}
                    confirmLabel={t`Delete rate`}
                    variant="destructive"
                    busy={deleteRate.isPending}
                    onClose={() => {
                        setDeletingRate(null);
                    }}
                    onConfirm={() => {
                        void (async () => {
                            try {
                                await deleteRate.mutateAsync({
                                    id: loan.id,
                                    partId: deletingRate.part.id,
                                    ratePeriodId: deletingRate.rate.id,
                                });
                                toast.success(t`Rate period deleted.`);
                                setDeletingRate(null);
                            } catch {
                                toast.error(t`Couldn't delete the rate period.`);
                            }
                        })();
                    }}
                />
            )}
            {deleting && (
                <ConfirmDialog
                    open
                    title={t`Delete this loan?`}
                    message={t`The loan definition, its parts, and rate history are removed. The accounts and every posted entry stay untouched.`}
                    confirmLabel={t`Delete loan`}
                    variant="destructive"
                    busy={deleteLoan.isPending}
                    onClose={() => {
                        setDeleting(false);
                    }}
                    onConfirm={() => {
                        void (async () => {
                            try {
                                await deleteLoan.mutateAsync(loan.id);
                                toast.success(t`Loan deleted.`);
                                await navigate({ to: '/loans' });
                            } catch {
                                toast.error(t`Couldn't delete the loan.`);
                            }
                        })();
                    }}
                />
            )}
        </div>
    );
}

function Kpi({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div className="flex flex-col gap-1">
            <span className="text-xs text-fg-3">{label}</span>
            {children}
        </div>
    );
}

function PartsTable({
    loan,
    onAddRate,
    onEditPart,
    onDeletePart,
    onEditRate,
    onDeleteRate,
}: {
    loan: LoanDetailModel;
    onAddRate: (part: LoanPart) => void;
    onEditPart: (part: LoanPart) => void;
    onDeletePart: (part: LoanPart) => void;
    onEditRate: (partId: LoanPartId, rate: LoanRatePeriod) => void;
    onDeleteRate: (part: LoanPart, rate: LoanRatePeriod) => void;
}) {
    const { t } = useLingui();
    const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());
    const canDeletePart = loan.parts.length > 1;

    const toggle = (id: string) => {
        setExpanded(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });
    };

    return (
        <div className="flex flex-col gap-2">
            {loan.parts.map(part => {
                const isOpen = expanded.has(part.id);
                return (
                    <div key={part.id} className="rounded-lg border border-border-soft">
                        <div className="px-3 py-2.5 grid grid-cols-[auto_minmax(0,1.4fr)_repeat(4,minmax(0,1fr))_auto] items-center gap-3">
                            <button
                                type="button"
                                onClick={() => {
                                    toggle(part.id);
                                }}
                                className="text-fg-3 hover:text-fg-1"
                                aria-label={
                                    isOpen ? t`Collapse rate history` : t`Show rate history`
                                }
                            >
                                <Icon
                                    name={isOpen ? 'chevron-down' : 'chevron-right'}
                                    size={15}
                                    strokeWidth={2}
                                />
                            </button>
                            <div className="min-w-0">
                                <div className="text-sm font-medium text-fg-1 truncate">
                                    {part.label}
                                </div>
                                <div className="text-xs text-fg-3 truncate">{part.accountName}</div>
                            </div>
                            <PartStat label={t`Type`} value={partTypeLabel(part.repaymentType)} />
                            <PartStat
                                label={t`Outstanding`}
                                value={
                                    <Amount
                                        minor={part.outstandingBalance}
                                        currencyCode={loan.currencyCode}
                                        size="inline"
                                    />
                                }
                            />
                            <PartStat
                                label={t`Rate`}
                                value={
                                    part.currentAnnualRatePercent === null
                                        ? '—'
                                        : `${part.currentAnnualRatePercent.toFixed(2)}%${fixedUntilSuffix(part)}`
                                }
                            />
                            <PartStat label={t`Runs until`} value={formatTableDate(part.endDate)} />
                            <div className="flex items-center gap-1">
                                <Button
                                    variant="ghost"
                                    onPress={() => {
                                        onEditPart(part);
                                    }}
                                    aria-label={t`Edit part`}
                                >
                                    <Icon name="pencil" size={14} strokeWidth={2} />
                                </Button>
                                <Button
                                    variant="ghost"
                                    onPress={() => {
                                        onDeletePart(part);
                                    }}
                                    isDisabled={!canDeletePart}
                                    aria-label={
                                        canDeletePart
                                            ? t`Delete part`
                                            : t`A loan must keep at least one part`
                                    }
                                >
                                    <Icon name="trash" size={14} strokeWidth={2} />
                                </Button>
                            </div>
                        </div>
                        {isOpen && (
                            <RateTimeline
                                part={part}
                                onAddRate={onAddRate}
                                onEditRate={onEditRate}
                                onDeleteRate={onDeleteRate}
                            />
                        )}
                    </div>
                );
            })}
        </div>
    );
}

/** A part's full effective-dated rate history, each row editable/removable (ADR-0026). */
function RateTimeline({
    part,
    onAddRate,
    onEditRate,
    onDeleteRate,
}: {
    part: LoanPart;
    onAddRate: (part: LoanPart) => void;
    onEditRate: (partId: LoanPartId, rate: LoanRatePeriod) => void;
    onDeleteRate: (part: LoanPart, rate: LoanRatePeriod) => void;
}) {
    const { t } = useLingui();
    const today = todayIso();
    const sorted = [...part.ratePeriods].sort((a, b) =>
        a.effectiveDate.localeCompare(b.effectiveDate),
    );
    // The rate in force today: latest effective on or before today.
    const inForceId = sorted.filter(r => r.effectiveDate <= today).at(-1)?.id ?? null;
    const canDeleteRate = sorted.length > 1;

    return (
        <div className="border-t border-border-soft px-3 py-2.5 flex flex-col gap-1.5 bg-surface-2/40">
            {sorted.map(rate => {
                const future = rate.effectiveDate > today;
                const inForce = rate.id === inForceId;
                return (
                    <div
                        key={rate.id}
                        className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-3 text-sm"
                    >
                        <div className="flex items-center gap-2 min-w-0">
                            <span className="tabular-nums font-medium">
                                {rate.annualRatePercent.toFixed(2)}%
                            </span>
                            <span className="text-fg-3 truncate">
                                <Trans>from {formatTableDate(rate.effectiveDate)}</Trans>
                                {rate.fixedUntil
                                    ? t` · fixed until ${formatTableDate(rate.fixedUntil)}`
                                    : ''}
                            </span>
                            {inForce && (
                                <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-brand-primary/15 text-brand-primary">
                                    <Trans>In force</Trans>
                                </span>
                            )}
                            {future && (
                                <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-surface-3 text-fg-3">
                                    <Trans>Future</Trans>
                                </span>
                            )}
                        </div>
                        <Button
                            variant="ghost"
                            onPress={() => {
                                onEditRate(part.id, rate);
                            }}
                            aria-label={t`Edit rate period`}
                        >
                            <Icon name="pencil" size={13} strokeWidth={2} />
                        </Button>
                        <Button
                            variant="ghost"
                            onPress={() => {
                                onDeleteRate(part, rate);
                            }}
                            isDisabled={!canDeleteRate}
                            aria-label={
                                canDeleteRate
                                    ? t`Delete rate period`
                                    : t`A part must keep at least one rate period`
                            }
                        >
                            <Icon name="trash" size={13} strokeWidth={2} />
                        </Button>
                    </div>
                );
            })}
            <div>
                <Button
                    variant="ghost"
                    onPress={() => {
                        onAddRate(part);
                    }}
                >
                    <Icon name="percent" size={13} strokeWidth={2} />
                    <Trans>New rate</Trans>
                </Button>
            </div>
        </div>
    );
}

function fixedUntilSuffix(part: LoanPart): string {
    const current = [...part.ratePeriods]
        .filter(r => r.effectiveDate <= todayIso())
        .sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate))[0];
    return current?.fixedUntil ? coreT` · fixed until ${formatTableDate(current.fixedUntil)}` : '';
}

function partTypeLabel(type: LoanPart['repaymentType']): string {
    return type === 'InterestOnly' ? coreT`Interest-only` : type;
}

function PartStat({ label, value }: { label: string; value: React.ReactNode }) {
    return (
        <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-[11px] text-fg-3">{label}</span>
            <span className="text-sm text-fg-1 truncate">{value}</span>
        </div>
    );
}
