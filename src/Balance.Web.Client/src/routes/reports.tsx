import { msg } from '@lingui/core/macro';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Insights } from '../screens/Insights';
import { defaultPeriod, parseIsoDate } from '../lib/reportPeriod';

const DEFAULT_CURRENCY = 'EUR';

type InsightsSearch = { from: string; to: string; currency: string };

export const Route = createFileRoute('/reports')({
    component: function ReportsRoute() {
        const { from, to, currency } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <Insights
                period={{ from, to }}
                currency={currency}
                onPeriodChange={period => {
                    void navigate({
                        search: prev => ({ ...prev, from: period.from, to: period.to }),
                    });
                }}
                onCurrencyChange={next => {
                    void navigate({ search: prev => ({ ...prev, currency: next }) });
                }}
            />
        );
    },
    staticData: { title: msg`Insights` },
    validateSearch: (raw: Record<string, unknown>): InsightsSearch => {
        const fallback = defaultPeriod();
        return {
            from: parseIsoDate(raw.from) ?? fallback.from,
            to: parseIsoDate(raw.to) ?? fallback.to,
            currency: typeof raw.currency === 'string' ? raw.currency : DEFAULT_CURRENCY,
        };
    },
});
