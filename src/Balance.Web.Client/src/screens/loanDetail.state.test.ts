import { describe, expect, it } from 'vitest';
import type { LoanPeriodRow, LoanProjection } from '../api/loans';
import { asAccountId, asLoanId, asLoanPartId } from '../lib/domain';
import {
    buildChartRows,
    buildScenario,
    buildScheduleYears,
    initialSimulator,
    type SimulatorState,
} from './loanDetail.state';

const partA = asLoanPartId('00000000-0000-7000-8000-00000000000a');
const partB = asLoanPartId('00000000-0000-7000-8000-00000000000b');

function row(
    period: string,
    loanPartId: typeof partA,
    overrides: Partial<LoanPeriodRow> = {},
): LoanPeriodRow {
    return {
        period,
        loanPartId,
        interest: 100,
        principal: 200,
        extraRepayment: 0,
        payment: 300,
        endBalance: 1_000,
        ...overrides,
    };
}

function projection(overrides: Partial<LoanProjection> = {}): LoanProjection {
    return {
        loanId: asLoanId('00000000-0000-7000-8000-000000000001'),
        currencyCode: 'EUR',
        anchorMonth: '2026-06-01',
        parts: [
            {
                id: partA,
                label: 'Part 1',
                accountId: asAccountId('00000000-0000-7000-8000-0000000000a1'),
                fixedUntil: null,
            },
        ],
        actuals: [],
        baseline: [],
        scenario: null,
        totals: null,
        ...overrides,
    };
}

describe('buildChartRows', () => {
    it('keeps actuals strictly before the anchor month and projection from it', () => {
        const rows = buildChartRows(
            projection({
                actuals: [
                    row('2026-04-01', partA, { endBalance: 1_200 }),
                    row('2026-05-01', partA, { endBalance: 1_100 }),
                    row('2026-06-01', partA, { endBalance: 1_050 }),
                ],
                baseline: [
                    row('2026-06-01', partA, { endBalance: 1_000 }),
                    row('2026-07-01', partA, { endBalance: 900 }),
                ],
            }),
        );

        expect(rows.map(r => r.period)).toEqual([
            '2026-04-01',
            '2026-05-01',
            '2026-06-01',
            '2026-07-01',
        ]);
        // The anchor month carries only the projection value — no double point.
        expect(rows[2]?.actual[partA]).toBeUndefined();
        expect(rows[2]?.proj[partA]).toBe(1_000);
        expect(rows[0]?.actual[partA]).toBe(1_200);
    });

    it('overlays the scenario as a per-period total across parts', () => {
        const rows = buildChartRows(
            projection({
                parts: [
                    {
                        id: partA,
                        label: 'Part 1',
                        accountId: asAccountId('00000000-0000-7000-8000-0000000000a1'),
                        fixedUntil: null,
                    },
                    {
                        id: partB,
                        label: 'Part 2',
                        accountId: asAccountId('00000000-0000-7000-8000-0000000000b1'),
                        fixedUntil: null,
                    },
                ],
                baseline: [row('2026-06-01', partA), row('2026-06-01', partB)],
                scenario: [
                    row('2026-06-01', partA, { endBalance: 700 }),
                    row('2026-06-01', partB, { endBalance: 250 }),
                ],
            }),
        );

        expect(rows[0]?.scenarioTotal).toBe(950);
    });
});

describe('buildScheduleYears', () => {
    it('groups months by year, marks fully-projected years, and totals per year', () => {
        const result = buildScheduleYears(
            projection({
                anchorMonth: '2026-02-01',
                actuals: [
                    row('2025-12-01', partA, { interest: 10, principal: 20 }),
                    row('2026-01-01', partA, { interest: 11, principal: 21 }),
                ],
                baseline: [
                    row('2026-02-01', partA, { interest: 12, principal: 22 }),
                    row('2027-01-01', partA, { interest: 13, principal: 23 }),
                ],
            }),
        );

        expect(result.map(y => y.year)).toEqual([2025, 2026, 2027]);
        expect(result[0]?.projected).toBe(false);
        expect(result[1]?.projected).toBe(false); // mixes actual January and projected rest
        expect(result[2]?.projected).toBe(true);
        expect(result[1]?.totalInterest).toBe(11 + 12);
        expect(result[1]?.totalPrincipal).toBe(21 + 22);
    });

    it('prefers scenario rows over the baseline for future months', () => {
        const result = buildScheduleYears(
            projection({
                baseline: [row('2026-06-01', partA, { principal: 200 })],
                scenario: [row('2026-06-01', partA, { principal: 200, extraRepayment: 500 })],
            }),
        );

        // Hypothetical extra repayments fold into the principal column.
        expect(result[0]?.months[0]?.cells[partA]?.principal).toBe(700);
    });
});

describe('buildScenario', () => {
    it('returns null while no repayment is complete and no assumed rate is set', () => {
        expect(buildScenario(initialSimulator('2026-06-07'), 2)).toBeNull();
    });

    it('projects complete repayments to minor units and keeps the policy', () => {
        const state: SimulatorState = {
            repayments: [
                { id: 'a', loanPartId: partA, date: '2026-08-01', amount: '5000' },
                { id: 'b', loanPartId: null, date: '2026-09-01', amount: '100' }, // incomplete
            ],
            policy: 'KeepPayment',
            assumedRatePercent: '',
        };

        const scenario = buildScenario(state, 2);

        expect(scenario).toEqual({
            extraRepayments: [{ loanPartId: partA, date: '2026-08-01', amount: 500_000 }],
            policy: 'KeepPayment',
            assumedAnnualRatePercent: null,
        });
    });

    it('an assumed post-fixation rate alone activates the scenario', () => {
        const state: SimulatorState = {
            ...initialSimulator('2026-06-07'),
            assumedRatePercent: '6.5',
        };

        const scenario = buildScenario(state, 2);

        expect(scenario?.assumedAnnualRatePercent).toBe(6.5);
        expect(scenario?.extraRepayments).toEqual([]);
    });
});
