import { useMemo, useState } from 'react';
import { Trans } from '@lingui/react/macro';
import { useCurrencyCatalog } from '../api/currencies';
import { type LoanProjection } from '../api/loans';
import { Icon } from '../components/Icon';
import { formatScheduleMonth } from '../lib/dates';
import { formatMoney } from '../lib/money';
import { buildScheduleYears, type ScheduleYear } from './loanDetail.state';

export function ScheduleTable({ projection }: { projection: LoanProjection }) {
    const years = useMemo(() => buildScheduleYears(projection), [projection]);
    const currentYear = Number(projection.anchorMonth.slice(0, 4));
    const [expanded, setExpanded] = useState<ReadonlySet<number>>(() => new Set([currentYear]));
    const catalog = useCurrencyCatalog();

    if (years.length === 0) {
        return (
            <div className="text-sm text-fg-3">
                <Trans>Nothing posted or projected yet.</Trans>
            </div>
        );
    }

    const fmt = (minor: number) => formatMoney(minor, projection.currencyCode, catalog);

    return (
        <div className="overflow-x-auto">
            <table className="w-full text-sm tabular-nums">
                <thead>
                    <tr className="text-left text-xs text-fg-3">
                        <th className="py-1.5 pr-3 font-medium">
                            <Trans>Period</Trans>
                        </th>
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
                            <Trans>Balance</Trans>
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
            <th className="py-1 px-3 font-normal border-l border-border-soft">
                <Trans>Interest</Trans>
            </th>
            <th className="py-1 px-3 font-normal">
                <Trans>Principal</Trans>
            </th>
            <th className="py-1 px-3 font-normal">
                <Trans>Balance</Trans>
            </th>
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
                                <Trans>Projected</Trans>
                            </span>
                        )}
                    </span>
                </td>
                <td
                    className="py-2 px-3 text-fg-2 border-l border-border-soft"
                    colSpan={parts.length * 3}
                >
                    <Trans>
                        {fmt(year.totalInterest)} interest · {fmt(year.totalPrincipal)} principal
                    </Trans>
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
                        <td className="py-1.5 pl-5 pr-3">{formatScheduleMonth(month.period)}</td>
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
