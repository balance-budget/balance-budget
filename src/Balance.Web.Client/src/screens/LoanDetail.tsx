import { useMemo, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useDeleteLoan,
    useDeleteLoanPart,
    useDeleteRatePeriod,
    useLoan,
    useLoanProjection,
    type LoanDetail as LoanDetailModel,
    type LoanPart,
    type LoanProjection,
    type LoanRatePeriod,
} from '../api/loans';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { LoanChart } from '../components/LoanChart';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Button } from '../components/ui/Button';
import { ComboBox } from '../components/ui/ComboBox';
import { DatePicker } from '../components/ui/DatePicker';
import { NumberField } from '../components/ui/NumberField';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { todayIso } from '../lib/dates';
import type { LoanId, LoanPartId } from '../lib/domain';
import { formatMoney } from '../lib/money';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import {
    AddLoanPartModal,
    AddRatePeriodModal,
    EditLoanModal,
    EditLoanPartModal,
    EditRatePeriodModal,
} from './LoanForm';
import {
    buildScenario,
    buildScheduleYears,
    emptyRepayment,
    initialSimulator,
    type ScheduleYear,
    type SimulatorState,
} from './loanDetail.state';

export function LoanDetail({ id }: { id: LoanId }) {
    const loan = useLoan(id);

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
                <ErrorState message="Couldn't load this loan." />
            </Panel>
        );
    }

    return <LoanDetailLoaded loan={loan.data} />;
}

function LoanDetailLoaded({ loan }: { loan: LoanDetailModel }) {
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
                    title={loan.name}
                    subtitle={`${loan.lenderName} · interest on ${loan.interestExpenseAccountName}`}
                    action={
                        <div className="flex items-center gap-2">
                            <Button
                                variant="secondary"
                                onPress={() => {
                                    setEditingLoan(true);
                                }}
                            >
                                <Icon name="pencil" size={14} strokeWidth={2} />
                                Edit
                            </Button>
                            <Button
                                variant="secondary"
                                onPress={() => {
                                    setAddingPart(true);
                                }}
                            >
                                <Icon name="plus" size={14} strokeWidth={2} />
                                Add part
                            </Button>
                            <Button
                                variant="danger"
                                onPress={() => {
                                    setDeleting(true);
                                }}
                            >
                                Delete
                            </Button>
                        </div>
                    }
                />
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <Kpi label="Outstanding">
                        <Amount
                            minor={loan.outstandingBalance}
                            currencyCode={loan.currencyCode}
                            size="medium"
                        />
                    </Kpi>
                    <Kpi label="Monthly payment">
                        <Amount
                            minor={loan.currentPayment}
                            currencyCode={loan.currencyCode}
                            size="medium"
                        />
                    </Kpi>
                    <Kpi label="Weighted rate">
                        <span className="text-xl font-medium tabular-nums">
                            {loan.weightedAnnualRatePercent === null
                                ? '—'
                                : `${loan.weightedAnnualRatePercent.toFixed(2)}%`}
                        </span>
                    </Kpi>
                    <Kpi label="Status">
                        <span className="text-xl font-medium">
                            {loan.isEnded ? 'Ended' : 'Active'}
                        </span>
                    </Kpi>
                </div>
                {loan.constructionDeposit && (
                    <div className="mt-3 rounded-lg bg-surface-2 px-3 py-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
                        <span className="inline-flex items-center gap-1.5 text-fg-2">
                            <Icon name="landmark" size={13} strokeWidth={2} />
                            Construction deposit
                        </span>
                        <span className="text-fg-3">
                            {loan.constructionDeposit.accountName} ·{' '}
                            <Amount
                                minor={loan.constructionDeposit.balance}
                                currencyCode={loan.currencyCode}
                                size="inline"
                            />{' '}
                            at {loan.constructionDeposit.annualRatePercent.toFixed(2)}% — interest
                            offsets the payment until it drains.
                        </span>
                    </div>
                )}
            </Panel>

            <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)] gap-4 items-start">
                <Panel>
                    <SectionHead
                        title="Balance & payments"
                        subtitle="Posted actuals to the left of today, the engine's projection to the right."
                    />
                    {projection.isPending ? (
                        <Skeleton className="h-[280px]" />
                    ) : projection.isError ? (
                        <ErrorState message="Couldn't compute the projection." />
                    ) : (
                        <LoanChart projection={projection.data} />
                    )}
                </Panel>

                <Panel>
                    <SectionHead
                        title="What if…"
                        subtitle="Hypothetical extra repayments — nothing is saved."
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
                    title="Parts"
                    subtitle="Parts — each with its own terms and rate history."
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
                    title="Schedule"
                    subtitle="Past rows are posted actuals; future rows are the projection (the what-if scenario when active)."
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
                    title={`Delete ${deletingPart.label}?`}
                    message="The part definition and its rate history are removed. Its account and every posted entry stay untouched — it becomes an ordinary account again."
                    confirmLabel="Delete part"
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
                                toast.success('Part deleted.');
                                setDeletingPart(null);
                            } catch {
                                toast.error("Couldn't delete the part.");
                            }
                        })();
                    }}
                />
            )}
            {deletingRate && (
                <ConfirmDialog
                    open
                    title="Delete this rate period?"
                    message={`The ${deletingRate.rate.annualRatePercent.toFixed(2)}% rate effective ${deletingRate.rate.effectiveDate} is removed from ${deletingRate.part.label}. Projections recompute; posted entries are untouched.`}
                    confirmLabel="Delete rate"
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
                                toast.success('Rate period deleted.');
                                setDeletingRate(null);
                            } catch {
                                toast.error("Couldn't delete the rate period.");
                            }
                        })();
                    }}
                />
            )}
            {deleting && (
                <ConfirmDialog
                    open
                    title="Delete this loan?"
                    message="The loan definition, its parts, and rate history are removed. The accounts and every posted entry stay untouched."
                    confirmLabel="Delete loan"
                    variant="destructive"
                    busy={deleteLoan.isPending}
                    onClose={() => {
                        setDeleting(false);
                    }}
                    onConfirm={() => {
                        void (async () => {
                            try {
                                await deleteLoan.mutateAsync(loan.id);
                                toast.success('Loan deleted.');
                                await navigate({ to: '/loans' });
                            } catch {
                                toast.error("Couldn't delete the loan.");
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
                                aria-label={isOpen ? 'Collapse rate history' : 'Show rate history'}
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
                            <PartStat label="Type" value={partTypeLabel(part.repaymentType)} />
                            <PartStat
                                label="Outstanding"
                                value={
                                    <Amount
                                        minor={part.outstandingBalance}
                                        currencyCode={loan.currencyCode}
                                        size="inline"
                                    />
                                }
                            />
                            <PartStat
                                label="Rate"
                                value={
                                    part.currentAnnualRatePercent === null
                                        ? '—'
                                        : `${part.currentAnnualRatePercent.toFixed(2)}%${fixedUntilSuffix(part)}`
                                }
                            />
                            <PartStat label="Runs until" value={part.endDate} />
                            <div className="flex items-center gap-1">
                                <Button
                                    variant="ghost"
                                    onPress={() => {
                                        onEditPart(part);
                                    }}
                                    aria-label="Edit part"
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
                                            ? 'Delete part'
                                            : 'A loan must keep at least one part'
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
                                from {rate.effectiveDate}
                                {rate.fixedUntil ? ` · fixed until ${rate.fixedUntil}` : ''}
                            </span>
                            {inForce && (
                                <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-brand-primary/15 text-brand-primary">
                                    In force
                                </span>
                            )}
                            {future && (
                                <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-surface-3 text-fg-3">
                                    Future
                                </span>
                            )}
                        </div>
                        <Button
                            variant="ghost"
                            onPress={() => {
                                onEditRate(part.id, rate);
                            }}
                            aria-label="Edit rate period"
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
                                    ? 'Delete rate period'
                                    : 'A part must keep at least one rate period'
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
                    New rate
                </Button>
            </div>
        </div>
    );
}

function fixedUntilSuffix(part: LoanPart): string {
    const current = [...part.ratePeriods]
        .filter(r => r.effectiveDate <= todayIso())
        .sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate))[0];
    return current?.fixedUntil ? ` · fixed until ${current.fixedUntil}` : '';
}

function partTypeLabel(type: LoanPart['repaymentType']): string {
    return type === 'InterestOnly' ? 'Interest-only' : type;
}

function PartStat({ label, value }: { label: string; value: React.ReactNode }) {
    return (
        <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-[11px] text-fg-3">{label}</span>
            <span className="text-sm text-fg-1 truncate">{value}</span>
        </div>
    );
}

// ---- simulator --------------------------------------------------------------------------------

function Simulator({
    loan,
    simulator,
    onChange,
    totals,
}: {
    loan: LoanDetailModel;
    simulator: SimulatorState;
    onChange: (next: SimulatorState) => void;
    totals: LoanProjection['totals'];
}) {
    const catalog = useCurrencyCatalog();
    const partItems = loan.parts.map(p => ({
        key: p.id,
        label: p.label,
        value: p.id,
    }));

    return (
        <div className="flex flex-col gap-3">
            {simulator.repayments.map(repayment => (
                <div
                    key={repayment.id}
                    className="flex flex-col gap-2 rounded-lg border border-border-soft p-2.5"
                >
                    <ComboBox
                        items={partItems}
                        value={repayment.loanPartId}
                        onChange={loanPartId => {
                            onChange({
                                ...simulator,
                                repayments: simulator.repayments.map(r =>
                                    r.id === repayment.id ? { ...r, loanPartId } : r,
                                ),
                            });
                        }}
                        placeholder="Loan part…"
                        ariaLabel="Loan part"
                    />
                    <div className="grid grid-cols-2 gap-2">
                        <DatePicker
                            aria-label="Repayment date"
                            value={repayment.date}
                            onChange={date => {
                                onChange({
                                    ...simulator,
                                    repayments: simulator.repayments.map(r =>
                                        r.id === repayment.id ? { ...r, date } : r,
                                    ),
                                });
                            }}
                        />
                        <NumberField
                            aria-label="Extra repayment amount"
                            value={repayment.amount ?? Number.NaN}
                            onChange={amount => {
                                onChange({
                                    ...simulator,
                                    repayments: simulator.repayments.map(r =>
                                        r.id === repayment.id
                                            ? { ...r, amount: Number.isNaN(amount) ? null : amount }
                                            : r,
                                    ),
                                });
                            }}
                            minValue={0}
                            formatOptions={{
                                style: 'currency',
                                currency: loan.currencyCode,
                                currencyDisplay: 'narrowSymbol',
                            }}
                            placeholder="Amount"
                            inputClassName="tabular-nums"
                        />
                    </div>
                </div>
            ))}
            <div className="flex items-center justify-between">
                <Button
                    variant="ghost"
                    onPress={() => {
                        onChange({
                            ...simulator,
                            repayments: [...simulator.repayments, emptyRepayment(todayIso())],
                        });
                    }}
                >
                    <Icon name="plus" size={13} strokeWidth={2} />
                    Add repayment
                </Button>
                {simulator.repayments.length > 1 && (
                    <Button
                        variant="ghost"
                        onPress={() => {
                            onChange({
                                ...simulator,
                                repayments: simulator.repayments.slice(0, -1),
                            });
                        }}
                    >
                        Remove last
                    </Button>
                )}
            </div>

            <div className="flex flex-col gap-1">
                <span className="text-xs font-medium text-fg-2">After repaying extra</span>
                <Select
                    aria-label="Repayment policy"
                    value={simulator.policy}
                    onChange={key => {
                        if (key !== null) {
                            onChange({
                                ...simulator,
                                policy: key as SimulatorState['policy'],
                            });
                        }
                    }}
                >
                    <SelectItem id="LowerPayment">Lower the monthly payment (default)</SelectItem>
                    <SelectItem id="KeepPayment">Keep the payment, finish earlier</SelectItem>
                </Select>
            </div>

            <TextField
                label="Assumed rate after fixation (%)"
                value={simulator.assumedRatePercent}
                onChange={assumedRatePercent => {
                    onChange({ ...simulator, assumedRatePercent });
                }}
                placeholder="Optional"
                inputClassName="tabular-nums"
            />

            {totals && (
                <div className="rounded-lg bg-surface-2 p-3 flex flex-col gap-1.5 text-sm">
                    <TotalsRow
                        label="Interest saved"
                        value={formatMoney(totals.interestSaved, loan.currencyCode, catalog)}
                    />
                    <TotalsRow
                        label="Payment change"
                        value={formatMoney(totals.nextPaymentDelta, loan.currencyCode, catalog, {
                            sign: true,
                        })}
                    />
                    <TotalsRow
                        label="End date"
                        value={
                            totals.scenarioEndDate === totals.baselineEndDate
                                ? (totals.baselineEndDate ?? '—')
                                : `${totals.baselineEndDate ?? '—'} → ${totals.scenarioEndDate ?? '—'}`
                        }
                    />
                </div>
            )}
        </div>
    );
}

function TotalsRow({ label, value }: { label: string; value: string }) {
    return (
        <div className="flex items-center justify-between gap-2">
            <span className="text-fg-3">{label}</span>
            <span className="font-medium tabular-nums">{value}</span>
        </div>
    );
}

// ---- schedule ----------------------------------------------------------------------------------

function ScheduleTable({ projection }: { projection: LoanProjection }) {
    const years = useMemo(() => buildScheduleYears(projection), [projection]);
    const currentYear = Number(projection.anchorMonth.slice(0, 4));
    const [expanded, setExpanded] = useState<ReadonlySet<number>>(() => new Set([currentYear]));
    const catalog = useCurrencyCatalog();

    if (years.length === 0) {
        return <div className="text-sm text-fg-3">Nothing posted or projected yet.</div>;
    }

    const fmt = (minor: number) => formatMoney(minor, projection.currencyCode, catalog);

    return (
        <div className="overflow-x-auto">
            <table className="w-full text-sm tabular-nums">
                <thead>
                    <tr className="text-left text-xs text-fg-3">
                        <th className="py-1.5 pr-3 font-medium">Period</th>
                        {projection.parts.map(p => (
                            <th
                                key={p.id}
                                colSpan={3}
                                className="py-1.5 px-3 font-medium border-l border-border-soft"
                            >
                                {p.label}
                            </th>
                        ))}
                        <th className="py-1.5 pl-3 font-medium text-right border-l border-border-soft">
                            Balance
                        </th>
                    </tr>
                    <tr className="text-left text-[11px] text-fg-3">
                        <th />
                        {projection.parts.map(p => (
                            <SubHeaders key={p.id} />
                        ))}
                        <th />
                    </tr>
                </thead>
                <tbody>
                    {years.map(year => (
                        <YearRows
                            key={year.year}
                            year={year}
                            parts={projection.parts}
                            expanded={expanded.has(year.year)}
                            onToggle={() => {
                                setExpanded(prev => {
                                    const next = new Set(prev);
                                    if (next.has(year.year)) next.delete(year.year);
                                    else next.add(year.year);
                                    return next;
                                });
                            }}
                            fmt={fmt}
                        />
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function SubHeaders() {
    return (
        <>
            <th className="py-1 px-3 font-normal border-l border-border-soft">Interest</th>
            <th className="py-1 px-3 font-normal">Principal</th>
            <th className="py-1 px-3 font-normal">Balance</th>
        </>
    );
}

function YearRows({
    year,
    parts,
    expanded,
    onToggle,
    fmt,
}: {
    year: ScheduleYear;
    parts: LoanProjection['parts'];
    expanded: boolean;
    onToggle: () => void;
    fmt: (minor: number) => string;
}) {
    const lastMonth = year.months[year.months.length - 1];
    return (
        <>
            <tr
                className="cursor-pointer border-t border-border-soft hover:bg-surface-2/60"
                onClick={onToggle}
            >
                <td className="py-2 pr-3 font-medium">
                    <span className="inline-flex items-center gap-1.5">
                        <Icon
                            name={expanded ? 'chevron-down' : 'chevron-right'}
                            size={13}
                            strokeWidth={2}
                            className="text-fg-3"
                        />
                        {year.year.toString()}
                        {year.projected && (
                            <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-surface-2 text-fg-3">
                                Projected
                            </span>
                        )}
                    </span>
                </td>
                <td
                    className="py-2 px-3 text-fg-2 border-l border-border-soft"
                    colSpan={parts.length * 3}
                >
                    {fmt(year.totalInterest)} interest · {fmt(year.totalPrincipal)} principal
                </td>
                <td className="py-2 pl-3 text-right font-medium border-l border-border-soft">
                    {lastMonth ? fmt(lastMonth.totalBalance) : '—'}
                </td>
            </tr>
            {expanded &&
                year.months.map(month => (
                    <tr
                        key={month.period}
                        className={`border-t border-border-soft/60 ${month.projected ? 'text-fg-2' : ''}`}
                    >
                        <td className="py-1.5 pl-5 pr-3">{month.period.slice(0, 7)}</td>
                        {parts.map(p => {
                            const cell = month.cells[p.id];
                            return cell ? (
                                <MonthCells key={p.id} fmt={fmt} cell={cell} />
                            ) : (
                                <EmptyCells key={p.id} />
                            );
                        })}
                        <td className="py-1.5 pl-3 text-right border-l border-border-soft">
                            {fmt(month.totalBalance)}
                        </td>
                    </tr>
                ))}
        </>
    );
}

function MonthCells({
    cell,
    fmt,
}: {
    cell: NonNullable<ScheduleYear['months'][number]['cells'][string]>;
    fmt: (minor: number) => string;
}) {
    return (
        <>
            <td className="py-1.5 px-3 border-l border-border-soft">{fmt(cell.interest)}</td>
            <td className="py-1.5 px-3">{fmt(cell.principal)}</td>
            <td className="py-1.5 px-3">{fmt(cell.endBalance)}</td>
        </>
    );
}

function EmptyCells() {
    return (
        <>
            <td className="py-1.5 px-3 border-l border-border-soft text-fg-3">—</td>
            <td className="py-1.5 px-3 text-fg-3">—</td>
            <td className="py-1.5 px-3 text-fg-3">—</td>
        </>
    );
}
