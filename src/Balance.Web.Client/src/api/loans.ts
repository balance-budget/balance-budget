import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import {
    asAccountId,
    asCounterpartyId,
    asLoanId,
    asLoanPartId,
    asLoanPartRatePeriodId,
    type AccountId,
    type CounterpartyId,
    type LoanId,
    type LoanPartId,
    type LoanPartRatePeriodId,
} from '../lib/domain';
import { deleteRequest, getJson, postJson } from '../lib/http';
import { toNumber } from '../lib/money';
import { accountsKeys } from './accounts';

type WireLoan = components['schemas']['LoanOutput'];
type WireLoanDetail = components['schemas']['LoanDetailOutput'];
type WireLoanPart = components['schemas']['LoanPartOutput'];
type WireRatePeriod = components['schemas']['LoanRatePeriodOutput'];
type WireProposal = components['schemas']['LoanPaymentProposalOutput'];
type WireProjection = components['schemas']['LoanProjectionOutput'];
type WirePeriodRow = components['schemas']['LoanPeriodRowOutput'];
export type WireCreateLoanRequest = components['schemas']['CreateLoanRequest'];
export type WireCreateLoanPartRequest = components['schemas']['CreateLoanPartRequest'];
export type WireRatePeriodRequest = components['schemas']['LoanRatePeriodRequest'];
export type WireLoanScenarioRequest = components['schemas']['LoanScenarioRequest'];

export type RepaymentType = components['schemas']['LoanRepaymentType'];
export type RepaymentPolicy = components['schemas']['ExtraRepaymentPolicy'];

export type Loan = {
    id: LoanId;
    name: string;
    lenderCounterpartyId: CounterpartyId;
    lenderName: string;
    interestExpenseAccountId: AccountId;
    parentAccountId: AccountId;
    currencyCode: string;
    outstandingBalance: number;
    currentPayment: number;
    weightedAnnualRatePercent: number | null;
    isEnded: boolean;
    partCount: number;
};

export type LoanRatePeriod = {
    id: LoanPartRatePeriodId;
    effectiveDate: string;
    annualRatePercent: number;
    fixedUntil: string | null;
};

export type LoanPart = {
    id: LoanPartId;
    label: string;
    repaymentType: RepaymentType;
    startDate: string;
    endDate: string;
    accountId: AccountId;
    accountName: string;
    outstandingBalance: number;
    currentAnnualRatePercent: number | null;
    isEnded: boolean;
    ratePeriods: LoanRatePeriod[];
};

export type LoanDetail = Omit<Loan, 'partCount'> & {
    interestExpenseAccountName: string;
    parts: LoanPart[];
};

export type LoanProposalLine = {
    loanPartId: LoanPartId;
    label: string;
    partAccountId: AccountId;
    interest: number;
    principal: number;
    payment: number;
};

export type LoanProposal = {
    loanId: LoanId;
    month: string;
    currencyCode: string;
    interestExpenseAccountId: AccountId;
    lines: LoanProposalLine[];
    total: number;
};

export type LoanPeriodRow = {
    period: string;
    loanPartId: LoanPartId;
    interest: number;
    principal: number;
    extraRepayment: number;
    payment: number;
    endBalance: number;
};

export type LoanProjectionPart = {
    id: LoanPartId;
    label: string;
    accountId: AccountId;
    fixedUntil: string | null;
};

export type LoanProjection = {
    loanId: LoanId;
    currencyCode: string;
    anchorMonth: string;
    parts: LoanProjectionPart[];
    actuals: LoanPeriodRow[];
    baseline: LoanPeriodRow[];
    scenario: LoanPeriodRow[] | null;
    totals: {
        interestSaved: number;
        nextPaymentDelta: number;
        baselineEndDate: string | null;
        scenarioEndDate: string | null;
    } | null;
};

export const loansKeys = {
    all: ['loans'] as const,
    list: () => [...loansKeys.all, 'list'] as const,
    detail: (id: LoanId) => [...loansKeys.all, 'detail', id] as const,
    proposal: (id: LoanId, month: string) => [...loansKeys.all, 'proposal', id, month] as const,
    projection: (id: LoanId, scenario: WireLoanScenarioRequest | null) =>
        [...loansKeys.all, 'projection', id, scenario] as const,
};

function toLoan(wire: WireLoan): Loan {
    return {
        id: asLoanId(wire.id),
        name: wire.name,
        lenderCounterpartyId: asCounterpartyId(wire.lenderCounterpartyId),
        lenderName: wire.lenderName,
        interestExpenseAccountId: asAccountId(wire.interestExpenseAccountId),
        parentAccountId: asAccountId(wire.parentAccountId),
        currencyCode: wire.currencyCode,
        outstandingBalance: toNumber(wire.outstandingBalance),
        currentPayment: toNumber(wire.currentPayment),
        weightedAnnualRatePercent:
            wire.weightedAnnualRatePercent === null ? null : Number(wire.weightedAnnualRatePercent),
        isEnded: wire.isEnded,
        partCount: Number(wire.partCount),
    };
}

function toRatePeriod(wire: WireRatePeriod): LoanRatePeriod {
    return {
        id: asLoanPartRatePeriodId(wire.id),
        effectiveDate: wire.effectiveDate,
        annualRatePercent: Number(wire.annualRatePercent),
        fixedUntil: wire.fixedUntil,
    };
}

function toLoanPart(wire: WireLoanPart): LoanPart {
    return {
        id: asLoanPartId(wire.id),
        label: wire.label,
        repaymentType: wire.repaymentType,
        startDate: wire.startDate,
        endDate: wire.endDate,
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        outstandingBalance: toNumber(wire.outstandingBalance),
        currentAnnualRatePercent:
            wire.currentAnnualRatePercent === null ? null : Number(wire.currentAnnualRatePercent),
        isEnded: wire.isEnded,
        ratePeriods: wire.ratePeriods.map(toRatePeriod),
    };
}

function toLoanDetail(wire: WireLoanDetail): LoanDetail {
    return {
        id: asLoanId(wire.id),
        name: wire.name,
        lenderCounterpartyId: asCounterpartyId(wire.lenderCounterpartyId),
        lenderName: wire.lenderName,
        interestExpenseAccountId: asAccountId(wire.interestExpenseAccountId),
        interestExpenseAccountName: wire.interestExpenseAccountName,
        parentAccountId: asAccountId(wire.parentAccountId),
        currencyCode: wire.currencyCode,
        outstandingBalance: toNumber(wire.outstandingBalance),
        currentPayment: toNumber(wire.currentPayment),
        weightedAnnualRatePercent:
            wire.weightedAnnualRatePercent === null ? null : Number(wire.weightedAnnualRatePercent),
        isEnded: wire.isEnded,
        parts: wire.parts.map(toLoanPart),
    };
}

function toPeriodRow(wire: WirePeriodRow): LoanPeriodRow {
    return {
        period: wire.period,
        loanPartId: asLoanPartId(wire.loanPartId),
        interest: toNumber(wire.interest),
        principal: toNumber(wire.principal),
        extraRepayment: toNumber(wire.extraRepayment),
        payment: toNumber(wire.payment),
        endBalance: toNumber(wire.endBalance),
    };
}

function toProjection(wire: WireProjection): LoanProjection {
    return {
        loanId: asLoanId(wire.loanId),
        currencyCode: wire.currencyCode,
        anchorMonth: wire.anchorMonth,
        parts: wire.parts.map(p => ({
            id: asLoanPartId(p.id),
            label: p.label,
            accountId: asAccountId(p.accountId),
            fixedUntil: p.fixedUntil,
        })),
        actuals: wire.actuals.map(toPeriodRow),
        baseline: wire.baseline.map(toPeriodRow),
        scenario: wire.scenario === null ? null : wire.scenario.map(toPeriodRow),
        totals:
            wire.totals === null
                ? null
                : {
                      interestSaved: toNumber(wire.totals.interestSaved),
                      nextPaymentDelta: toNumber(wire.totals.nextPaymentDelta),
                      baselineEndDate: wire.totals.baselineEndDate,
                      scenarioEndDate: wire.totals.scenarioEndDate,
                  },
    };
}

export function useLoans() {
    return useQuery({
        queryKey: loansKeys.list(),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireLoan[]>('/api/loans', signal, 'load loans');
            return wire.map(toLoan);
        },
    });
}

export function useLoan(id: LoanId) {
    return useQuery({
        queryKey: loansKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireLoanDetail>(`/api/loans/${id}`, signal, 'load loan');
            return toLoanDetail(wire);
        },
    });
}

/**
 * Engine-computed pre-fill for the loan-aware categorise mode. `month` is any
 * date in the target month (the BT booking date).
 */
export function useLoanPaymentProposal(id: LoanId | null, month: string) {
    return useQuery({
        queryKey: id ? loansKeys.proposal(id, month) : ['loans', 'proposal', 'noop'],
        queryFn: async ({ signal }) => {
            if (id === null) return null;
            const wire = await getJson<WireProposal>(
                `/api/loans/${id}/payment-proposal?month=${encodeURIComponent(month)}`,
                signal,
                'load loan payment proposal',
            );
            return {
                loanId: asLoanId(wire.loanId),
                month: wire.month,
                currencyCode: wire.currencyCode,
                interestExpenseAccountId: asAccountId(wire.interestExpenseAccountId),
                lines: wire.lines.map(l => ({
                    loanPartId: asLoanPartId(l.loanPartId),
                    label: l.label,
                    partAccountId: asAccountId(l.partAccountId),
                    interest: toNumber(l.interest),
                    principal: toNumber(l.principal),
                    payment: toNumber(l.payment),
                })),
                total: toNumber(wire.total),
            } satisfies LoanProposal;
        },
        enabled: id !== null,
    });
}

/**
 * Actuals + baseline + optional what-if overlay. The scenario is part of the
 * query key, so the simulator panel re-fetches as the (debounced) scenario
 * changes — there is deliberately no client-side amortization math (ADR-0025).
 */
export function useLoanProjection(id: LoanId, scenario: WireLoanScenarioRequest | null) {
    return useQuery({
        queryKey: loansKeys.projection(id, scenario),
        queryFn: async ({ signal }) => {
            const wire = await postJson<WireProjection>(
                `/api/loans/${id}/projection`,
                { scenario },
                signal,
                'load loan projection',
            );
            return toProjection(wire);
        },
        placeholderData: previous => previous,
    });
}

export function useCreateLoan() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireCreateLoanRequest) => {
            const wire = await postJson<WireLoanDetail>(
                '/api/loans',
                request,
                new AbortController().signal,
                'create loan',
            );
            return toLoanDetail(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: loansKeys.all });
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
        },
    });
}

export function useDeleteLoan() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: LoanId) => {
            await deleteRequest(`/api/loans/${id}`, new AbortController().signal, 'delete loan');
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: loansKeys.all });
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
        },
    });
}

export function useAddLoanPart() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: { id: LoanId; request: WireCreateLoanPartRequest }) => {
            const wire = await postJson<WireLoanDetail>(
                `/api/loans/${args.id}/parts`,
                args.request,
                new AbortController().signal,
                'add loan part',
            );
            return toLoanDetail(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: loansKeys.all });
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
        },
    });
}

export function useAddRatePeriod() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            id: LoanId;
            partId: LoanPartId;
            request: WireRatePeriodRequest;
        }) => {
            const wire = await postJson<WireLoanDetail>(
                `/api/loans/${args.id}/parts/${args.partId}/rate-periods`,
                args.request,
                new AbortController().signal,
                'add rate period',
            );
            return toLoanDetail(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: loansKeys.all });
        },
    });
}
