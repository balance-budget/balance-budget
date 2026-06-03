import { CurrencySelect } from '../components/CurrencySelect';
import { DistributionChart } from '../components/DistributionChart';
import { MoneyFlowChart } from '../components/MoneyFlowChart';
import { Panel } from '../components/Panel';
import { PeriodPicker } from '../components/PeriodPicker';
import type { ReportPeriod } from '../lib/reportPeriod';

type InsightsProps = {
    period: ReportPeriod;
    currency: string;
    onPeriodChange: (period: ReportPeriod) => void;
    onCurrencyChange: (currency: string) => void;
};

/**
 * Insights: the date-ranged reporting area. A shared Reporting period and
 * Currency at the top scope both Reports below — the Distribution and the Money
 * flow. Period and currency live in the route's search params, so a view is
 * shareable; the Distribution's type toggle and drill-down are local.
 */
export function Insights({ period, currency, onPeriodChange, onCurrencyChange }: InsightsProps) {
    return (
        <>
            <Panel padding="sm">
                <div className="flex flex-wrap items-center justify-between gap-3">
                    <PeriodPicker period={period} onChange={onPeriodChange} />
                    <CurrencySelect value={currency} onChange={onCurrencyChange} />
                </div>
            </Panel>

            <DistributionChart period={period} currency={currency} />
            <MoneyFlowChart period={period} currency={currency} />
        </>
    );
}
