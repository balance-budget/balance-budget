import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import {
    asAccountId,
    asCounterpartyId,
    asJournalEntryTemplateId,
    type AccountId,
    type AccountType,
    type CounterpartyId,
    type JournalEntryTemplateId,
} from '../lib/domain';
import { deleteRequest, getJson, postJson, putJson } from '../lib/http';
import { toNumber } from '../lib/money';
import { accountsKeys } from './accounts';

type WireTemplate = components['schemas']['JournalEntryTemplateOutput'];
type WireCandidate = components['schemas']['TemplateCandidateOutput'];
type WireProjection = components['schemas']['OutlookProjectionOutput'];
type WireAccountProjection = components['schemas']['OutlookAccountProjectionOutput'];
type WireActualPoint = components['schemas']['OutlookActualPointOutput'];
type WireProjectedMonth = components['schemas']['OutlookProjectedMonthOutput'];
type WireThisMonth = components['schemas']['OutlookThisMonthOutput'];
type WireYearEnd = components['schemas']['OutlookYearEndOutput'];

export type WireCreateTemplateRequest = components['schemas']['CreateJournalEntryTemplateRequest'];
export type WireUpdateTemplateRequest = components['schemas']['UpdateJournalEntryTemplateRequest'];
export type WireScenarioRequest = components['schemas']['OutlookScenarioRequest'];

export type Cadence = components['schemas']['Cadence'];

export type JournalEntryTemplate = {
    id: JournalEntryTemplateId;
    name: string;
    accountId: AccountId;
    accountName: string;
    counterAccountId: AccountId | null;
    counterAccountName: string | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    cadence: Cadence;
    anchorDate: string;
    endDate: string | null;
    expectedAmount: number;
    monthlyEquivalent: number;
    nextDueDate: string | null;
    currencyCode: string;
    mandateId: string | null;
    sepaCreditorId: string | null;
};

export type TemplateCandidate = {
    accountId: AccountId;
    accountName: string;
    counterAccountId: AccountId | null;
    counterAccountName: string | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    suggestedName: string;
    cadence: Cadence;
    anchorDate: string;
    expectedAmount: number;
    monthlyEquivalent: number;
    occurrenceCount: number;
    currencyCode: string;
    mandateId: string | null;
    sepaCreditorId: string | null;
};

export type OutlookActualPoint = { month: string; endBalance: number };

export type OutlookProjectedMonth = {
    month: string;
    expectedIn: number;
    expectedOut: number;
    expectedNet: number;
    typicalSpendMid: number;
    endBalanceLow: number;
    endBalanceMid: number;
    endBalanceHigh: number;
};

export type OutlookThisMonth = {
    month: string;
    expectedIn: number;
    expectedOut: number;
    everydaySpendLow: number;
    everydaySpendHigh: number;
    endBalanceLow: number;
    endBalanceMid: number;
    endBalanceHigh: number;
};

export type OutlookYearEnd = {
    date: string;
    endBalanceLow: number;
    endBalanceMid: number;
    endBalanceHigh: number;
};

export type OutlookAccountProjection = {
    accountId: AccountId;
    accountName: string;
    accountType: AccountType;
    currencyCode: string;
    currentBalance: number;
    thisMonth: OutlookThisMonth;
    yearEnd: OutlookYearEnd;
    actuals: OutlookActualPoint[];
    baseline: OutlookProjectedMonth[];
    scenario: OutlookProjectedMonth[] | null;
};

export type OutlookProjection = {
    anchorMonth: string;
    horizonMonths: number;
    accounts: OutlookAccountProjection[];
};

export const outlookKeys = {
    all: ['outlook'] as const,
    templates: () => [...outlookKeys.all, 'templates'] as const,
    candidates: () => [...outlookKeys.all, 'candidates'] as const,
    projection: (currency: string, horizon: number, scenario: WireScenarioRequest | null) =>
        [...outlookKeys.all, 'projection', currency, horizon, scenario] as const,
};

function toTemplate(wire: WireTemplate): JournalEntryTemplate {
    return {
        id: asJournalEntryTemplateId(wire.id),
        name: wire.name,
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        counterAccountId:
            wire.counterAccountId === null ? null : asAccountId(wire.counterAccountId),
        counterAccountName: wire.counterAccountName,
        counterpartyId: wire.counterpartyId === null ? null : asCounterpartyId(wire.counterpartyId),
        counterpartyName: wire.counterpartyName,
        cadence: wire.cadence,
        anchorDate: wire.anchorDate,
        endDate: wire.endDate,
        expectedAmount: toNumber(wire.expectedAmount),
        monthlyEquivalent: toNumber(wire.monthlyEquivalent),
        nextDueDate: wire.nextDueDate,
        currencyCode: wire.currencyCode,
        mandateId: wire.mandateId,
        sepaCreditorId: wire.sepaCreditorId,
    };
}

function toCandidate(wire: WireCandidate): TemplateCandidate {
    return {
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        counterAccountId:
            wire.counterAccountId === null ? null : asAccountId(wire.counterAccountId),
        counterAccountName: wire.counterAccountName,
        counterpartyId: wire.counterpartyId === null ? null : asCounterpartyId(wire.counterpartyId),
        counterpartyName: wire.counterpartyName,
        suggestedName: wire.suggestedName,
        cadence: wire.cadence,
        anchorDate: wire.anchorDate,
        expectedAmount: toNumber(wire.expectedAmount),
        monthlyEquivalent: toNumber(wire.monthlyEquivalent),
        occurrenceCount: Number(wire.occurrenceCount),
        currencyCode: wire.currencyCode,
        mandateId: wire.mandateId,
        sepaCreditorId: wire.sepaCreditorId,
    };
}

function toProjectedMonth(wire: WireProjectedMonth): OutlookProjectedMonth {
    return {
        month: wire.month,
        expectedIn: toNumber(wire.expectedIn),
        expectedOut: toNumber(wire.expectedOut),
        expectedNet: toNumber(wire.expectedNet),
        typicalSpendMid: toNumber(wire.typicalSpendMid),
        endBalanceLow: toNumber(wire.endBalanceLow),
        endBalanceMid: toNumber(wire.endBalanceMid),
        endBalanceHigh: toNumber(wire.endBalanceHigh),
    };
}

function toThisMonth(wire: WireThisMonth): OutlookThisMonth {
    return {
        month: wire.month,
        expectedIn: toNumber(wire.expectedIn),
        expectedOut: toNumber(wire.expectedOut),
        everydaySpendLow: toNumber(wire.everydaySpendLow),
        everydaySpendHigh: toNumber(wire.everydaySpendHigh),
        endBalanceLow: toNumber(wire.endBalanceLow),
        endBalanceMid: toNumber(wire.endBalanceMid),
        endBalanceHigh: toNumber(wire.endBalanceHigh),
    };
}

function toYearEnd(wire: WireYearEnd): OutlookYearEnd {
    return {
        date: wire.date,
        endBalanceLow: toNumber(wire.endBalanceLow),
        endBalanceMid: toNumber(wire.endBalanceMid),
        endBalanceHigh: toNumber(wire.endBalanceHigh),
    };
}

function toActualPoint(wire: WireActualPoint): OutlookActualPoint {
    return { month: wire.month, endBalance: toNumber(wire.endBalance) };
}

function toAccountProjection(wire: WireAccountProjection): OutlookAccountProjection {
    return {
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        accountType: wire.accountType,
        currencyCode: wire.currencyCode,
        currentBalance: toNumber(wire.currentBalance),
        thisMonth: toThisMonth(wire.thisMonth),
        yearEnd: toYearEnd(wire.yearEnd),
        actuals: wire.actuals.map(toActualPoint),
        baseline: wire.baseline.map(toProjectedMonth),
        scenario: wire.scenario === null ? null : wire.scenario.map(toProjectedMonth),
    };
}

function toProjection(wire: WireProjection): OutlookProjection {
    return {
        anchorMonth: wire.anchorMonth,
        horizonMonths: Number(wire.horizonMonths),
        accounts: wire.accounts.map(toAccountProjection),
    };
}

export function useOutlookTemplates() {
    return useQuery({
        queryKey: outlookKeys.templates(),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireTemplate[]>(
                '/api/outlook/templates',
                signal,
                'load recurring items',
            );
            return wire.map(toTemplate);
        },
    });
}

export function useTemplateCandidates() {
    return useQuery({
        queryKey: outlookKeys.candidates(),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireCandidate[]>(
                '/api/outlook/candidates',
                signal,
                'load detected recurring items',
            );
            return wire.map(toCandidate);
        },
    });
}

/**
 * Actuals + baseline projection, with an optional ephemeral what-if scenario.
 * The scenario is part of the query key so the chart re-fetches as levers
 * change; there is deliberately no client-side projection math (ADR-0027).
 */
export function useOutlookProjection(
    currencyCode: string,
    horizonMonths: number,
    scenario: WireScenarioRequest | null,
) {
    return useQuery({
        queryKey: outlookKeys.projection(currencyCode, horizonMonths, scenario),
        queryFn: async ({ signal }) => {
            const wire = await postJson<WireProjection>(
                `/api/outlook/projection?currency=${encodeURIComponent(currencyCode)}&horizon=${horizonMonths}`,
                { scenario },
                signal,
                'load outlook projection',
            );
            return toProjection(wire);
        },
        placeholderData: previous => previous,
    });
}

export function useCreateTemplate() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireCreateTemplateRequest) => {
            const wire = await postJson<WireTemplate>(
                '/api/outlook/templates',
                request,
                new AbortController().signal,
                'create recurring item',
            );
            return toTemplate(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: outlookKeys.all });
        },
    });
}

export function useUpdateTemplate() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            id: JournalEntryTemplateId;
            request: WireUpdateTemplateRequest;
        }) => {
            const wire = await putJson<WireTemplate>(
                `/api/outlook/templates/${args.id}`,
                args.request,
                new AbortController().signal,
                'update recurring item',
            );
            return toTemplate(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: outlookKeys.all });
        },
    });
}

export function useDeleteTemplate() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: JournalEntryTemplateId) => {
            await deleteRequest(
                `/api/outlook/templates/${id}`,
                new AbortController().signal,
                'delete recurring item',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: outlookKeys.all });
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
        },
    });
}
