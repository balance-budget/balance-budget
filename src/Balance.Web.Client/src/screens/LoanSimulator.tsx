import { Trans, useLingui } from '@lingui/react/macro';
import { useCurrencyCatalog } from '../api/currencies';
import { type LoanDetail as LoanDetailModel, type LoanProjection } from '../api/loans';
import { Icon } from '../components/Icon';
import { Button } from '../components/ui/Button';
import { ComboBox } from '../components/ui/ComboBox';
import { DatePicker } from '../components/ui/DatePicker';
import { NumberField } from '../components/ui/NumberField';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { formatTableDate, todayIso } from '../lib/dates';
import { formatMoney } from '../lib/money';
import { emptyRepayment, type SimulatorState } from './loanDetail.state';

/** A nullable payoff date as a region-aware calendar date, or an em dash when open-ended. */
function formatEndDate(date: string | null): string {
    return date ? formatTableDate(date) : '—';
}

export function Simulator({
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
    const { t } = useLingui();
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
                        placeholder={t`Loan part…`}
                        ariaLabel={t`Loan part`}
                    />
                    <div className="grid grid-cols-2 gap-2">
                        <DatePicker
                            aria-label={t`Repayment date`}
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
                            aria-label={t`Extra repayment amount`}
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
                            placeholder={t`Amount`}
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
                    <Trans>Add repayment</Trans>
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
                        <Trans>Remove last</Trans>
                    </Button>
                )}
            </div>

            <div className="flex flex-col gap-1">
                <span className="text-xs font-medium text-fg-2">
                    <Trans>After repaying extra</Trans>
                </span>
                <Select
                    aria-label={t`Repayment policy`}
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
                    <SelectItem
                        id="LowerPayment"
                        textValue={t`Lower the monthly payment (default)`}
                    >
                        <Trans>Lower the monthly payment (default)</Trans>
                    </SelectItem>
                    <SelectItem id="KeepPayment" textValue={t`Keep the payment, finish earlier`}>
                        <Trans>Keep the payment, finish earlier</Trans>
                    </SelectItem>
                </Select>
            </div>

            <TextField
                label={t`Assumed rate after fixation (%)`}
                value={simulator.assumedRatePercent}
                onChange={assumedRatePercent => {
                    onChange({ ...simulator, assumedRatePercent });
                }}
                placeholder={t`Optional`}
                inputClassName="tabular-nums"
            />

            {totals && (
                <div className="rounded-lg bg-surface-2 p-3 flex flex-col gap-1.5 text-sm">
                    <TotalsRow
                        label={t`Interest saved`}
                        value={formatMoney(totals.interestSaved, loan.currencyCode, catalog)}
                    />
                    <TotalsRow
                        label={t`Payment change`}
                        value={formatMoney(totals.nextPaymentDelta, loan.currencyCode, catalog, {
                            sign: true,
                        })}
                    />
                    <TotalsRow
                        label={t`End date`}
                        value={
                            totals.scenarioEndDate === totals.baselineEndDate
                                ? formatEndDate(totals.baselineEndDate)
                                : `${formatEndDate(totals.baselineEndDate)} → ${formatEndDate(totals.scenarioEndDate)}`
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
