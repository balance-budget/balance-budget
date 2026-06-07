import { useMemo, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useDeleteLoan,
    useLoan,
    useLoanProjection,
    type LoanDetail as LoanDetailModel,
    type LoanPart,
    type LoanProjection,
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
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { todayIso } from '../lib/dates';
import type { LoanId } from '../lib/domain';
import { formatMoney } from '../lib/money';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import { AddLoanPartModal, AddRatePeriodModal } from './LoanForm';
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
    const [simulator, setSimulator] = useState<SimulatorState>(() => initialSimulator(todayIso()));
    const [addingPart, setAddingPart] = useState(false);
    const [ratePart, setRatePart] = useState<LoanPart | null>(null);
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
            </Panel>

            <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)] gap-4 items-start">
                <Panel>
                    <SectionHead
                        title="Balance over time"
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
                    subtitle="Leningdelen — each with its own terms and rate history."
                />
                <PartsTable
                    loan={loan}
                    onAddRate={part => {
                        setRatePart(part);
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
}: {
    loan: LoanDetailModel;
    onAddRate: (part: LoanPart) => void;
}) {
    return (
        <div className="flex flex-col gap-2">
            {loan.parts.map(part => (
                <div
                    key={part.id}
                    className="rounded-lg border border-border-soft px-3 py-2.5 grid grid-cols-[minmax(0,1.4fr)_repeat(4,minmax(0,1fr))_auto] items-center gap-3"
                >
                    <div className="min-w-0">
                        <div className="text-sm font-medium text-fg-1 truncate">{part.label}</div>
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
            ))}
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
                        <TextField
                            aria-label="Extra repayment amount"
                            value={repayment.amount}
                            onChange={amount => {
                                onChange({
                                    ...simulator,
                                    repayments: simulator.repayments.map(r =>
                                        r.id === repayment.id ? { ...r, amount } : r,
                                    ),
                                });
                            }}
                            placeholder={`Amount (${loan.currencyCode})`}
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
                    <SelectItem id="LowerPayment">
                        Lower the monthly payment (NL default)
                    </SelectItem>
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
