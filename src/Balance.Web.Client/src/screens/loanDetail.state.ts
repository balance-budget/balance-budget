/*
 * Pure helpers for the Loan detail screen: merging ledger actuals with the
 * engine projection into chart rows and a year-grouped schedule table, plus
 * the simulator's scenario construction. No amortization math happens here —
 * the engine on the server is the single implementation (ADR-0025).
 */

import type { LoanPeriodRow, LoanProjection, RepaymentPolicy } from '../api/loans';
import type { LoanPartId } from '../lib/domain';
import type { components } from '../lib/api-types.gen';

type WireScenario = components['schemas']['LoanScenarioRequest'];

export type ChartRow = {
    period: string;
    /** Stacked per-part balances; key = LoanPartId. Projection months only carry `proj`. */
    actual: Record<string, number>;
    proj: Record<string, number>;
    scenarioTotal: number | null;
};

export type PaymentChartRow = {
    period: string;
    /** Per-part repayment (principal + extra) for the period; key = LoanPartId. */
    repay: Record<string, number>;
    /** Per-part interest for the period; key = LoanPartId. */
    interest: Record<string, number>;
};

/**
 * The monthly payment composition over time: per part, repayment and interest.
 * Actual months left of the anchor come from the ledger, the rest from the
 * projection (scenario when active). Principal is floored at zero so the
 * opening/borrowing month (a large negative "repayment") doesn't distort the
 * flow view — that event belongs to the balance chart, not the payment one.
 */
export function buildPaymentRows(projection: LoanProjection): PaymentChartRow[] {
    const byPeriod = new Map<string, PaymentChartRow>();
    const row = (period: string): PaymentChartRow => {
        let existing = byPeriod.get(period);
        if (!existing) {
            existing = { period, repay: {}, interest: {} };
            byPeriod.set(period, existing);
        }
        return existing;
    };

    const fill = (r: LoanPeriodRow) => {
        const rr = row(r.period);
        rr.repay[r.loanPartId] = Math.max(0, r.principal + r.extraRepayment);
        rr.interest[r.loanPartId] = Math.max(0, r.interest);
    };

    for (const r of projection.actuals) {
        if (r.period >= projection.anchorMonth) continue;
        fill(r);
    }
    for (const r of projection.scenario ?? projection.baseline) {
        fill(r);
    }

    return [...byPeriod.values()].sort((a, b) => a.period.localeCompare(b.period));
}

export type ScheduleCell = {
    interest: number;
    principal: number; // includes hypothetical extra repayments in scenario rows
    endBalance: number;
};

export type ScheduleMonth = {
    period: string;
    projected: boolean;
    cells: Record<string, ScheduleCell | undefined>;
    totalInterest: number;
    totalPrincipal: number;
    totalBalance: number;
};

export type ScheduleYear = {
    year: number;
    projected: boolean;
    months: ScheduleMonth[];
    totalInterest: number;
    totalPrincipal: number;
};

/**
 * One continuous month axis: actual months strictly before the anchor month,
 * projection from the anchor onward. The anchor month appears once, as
 * projection — its posted payment (if any) is already inside the anchor
 * balance the engine projected from.
 */
export function buildChartRows(projection: LoanProjection): ChartRow[] {
    const byPeriod = new Map<string, ChartRow>();

    const row = (period: string): ChartRow => {
        let existing = byPeriod.get(period);
        if (!existing) {
            existing = { period, actual: {}, proj: {}, scenarioTotal: null };
            byPeriod.set(period, existing);
        }
        return existing;
    };

    for (const r of projection.actuals) {
        if (r.period >= projection.anchorMonth) continue;
        row(r.period).actual[r.loanPartId] = r.endBalance;
    }
    for (const r of projection.baseline) {
        row(r.period).proj[r.loanPartId] = r.endBalance;
    }
    if (projection.scenario) {
        const totals = new Map<string, number>();
        for (const r of projection.scenario) {
            totals.set(r.period, (totals.get(r.period) ?? 0) + r.endBalance);
        }
        for (const [period, total] of totals) {
            row(period).scenarioTotal = total;
        }
    }

    return [...byPeriod.values()].sort((a, b) => a.period.localeCompare(b.period));
}

/**
 * Year-grouped schedule: past rows are posted actuals, rows from the anchor
 * month on are the projection (scenario rows when a scenario is active, so
 * the table answers "what would happen"). A year mixing both counts as
 * not-projected so it lands in the expanded-by-default group.
 */
export function buildScheduleYears(projection: LoanProjection): ScheduleYear[] {
    const months = new Map<string, ScheduleMonth>();

    const monthRow = (period: string, projected: boolean): ScheduleMonth => {
        let existing = months.get(period);
        if (!existing) {
            existing = {
                period,
                projected,
                cells: {},
                totalInterest: 0,
                totalPrincipal: 0,
                totalBalance: 0,
            };
            months.set(period, existing);
        }
        return existing;
    };

    const add = (rows: LoanPeriodRow[], projected: boolean) => {
        for (const r of rows) {
            const m = monthRow(r.period, projected);
            const principal = r.principal + r.extraRepayment;
            m.cells[r.loanPartId] = {
                interest: r.interest,
                principal,
                endBalance: r.endBalance,
            };
            m.totalInterest += r.interest;
            m.totalPrincipal += principal;
            m.totalBalance += r.endBalance;
        }
    };

    add(
        projection.actuals.filter(r => r.period < projection.anchorMonth),
        false,
    );
    add(projection.scenario ?? projection.baseline, true);

    const years = new Map<number, ScheduleYear>();
    for (const m of [...months.values()].sort((a, b) => a.period.localeCompare(b.period))) {
        const year = Number(m.period.slice(0, 4));
        let group = years.get(year);
        if (!group) {
            group = { year, projected: true, months: [], totalInterest: 0, totalPrincipal: 0 };
            years.set(year, group);
        }
        group.months.push(m);
        group.projected = group.projected && m.projected;
        group.totalInterest += m.totalInterest;
        group.totalPrincipal += m.totalPrincipal;
    }

    return [...years.values()].sort((a, b) => a.year - b.year);
}

// ---- simulator ------------------------------------------------------------------------------

export type SimulatorRepayment = {
    id: string;
    loanPartId: LoanPartId | null;
    date: string; // ISO yyyy-MM-dd
    amount: number | null; // major-units, from the currency NumberField (null when empty)
};

export type SimulatorState = {
    repayments: SimulatorRepayment[];
    policy: RepaymentPolicy;
    assumedRatePercent: string;
};

let simCounter = 0;
export function nextRepaymentId(): string {
    simCounter += 1;
    return `sim-${simCounter.toString(36)}`;
}

export function emptyRepayment(defaultDate: string): SimulatorRepayment {
    return { id: nextRepaymentId(), loanPartId: null, date: defaultDate, amount: null };
}

export function initialSimulator(defaultDate: string): SimulatorState {
    return {
        repayments: [emptyRepayment(defaultDate)],
        policy: 'LowerPayment',
        assumedRatePercent: '',
    };
}

/**
 * Project the simulator's inputs onto the wire scenario; `null` while nothing
 * is complete yet (no extra repayment fully entered), so the screen keeps
 * showing the plain baseline without a redundant scenario request.
 */
export function buildScenario(state: SimulatorState, scale: number): WireScenario | null {
    const extraRepayments: WireScenario['extraRepayments'] = [];
    for (const r of state.repayments) {
        if (r.loanPartId === null || r.date === '' || r.amount === null) continue;
        if (!Number.isFinite(r.amount) || r.amount <= 0) continue;
        const minor = Math.round(r.amount * 10 ** scale);
        if (minor <= 0) continue;
        extraRepayments.push({ loanPartId: r.loanPartId, date: r.date, amount: minor });
    }

    const assumedTrimmed = state.assumedRatePercent.trim();
    const assumedRate = assumedTrimmed === '' ? null : Number(assumedTrimmed);
    const hasAssumedRate = assumedRate !== null && Number.isFinite(assumedRate) && assumedRate >= 0;

    if (extraRepayments.length === 0 && !hasAssumedRate) return null;

    return {
        extraRepayments,
        policy: state.policy,
        assumedAnnualRatePercent: hasAssumedRate ? assumedRate : null,
    };
}
