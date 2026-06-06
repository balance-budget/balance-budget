import { cx } from '../lib/cx';
import { detectPreset, PERIOD_PRESETS, presetRange, type ReportPeriod } from '../lib/reportPeriod';
import { DateField } from './DateField';

type PeriodPickerProps = {
    period: ReportPeriod;
    onChange: (period: ReportPeriod) => void;
};

/**
 * Reporting-period control: preset pills plus a pair of custom date inputs.
 * Presets just compute an [from, to] range and hand it up; the active pill is
 * derived by matching the current period against each preset, so a custom range
 * (or a shared URL) lights up "Custom".
 */
export function PeriodPicker({ period, onChange }: PeriodPickerProps) {
    const active = detectPreset(period);

    return (
        <div className="flex flex-wrap items-center gap-2">
            <div className="flex flex-wrap items-center gap-[6px]">
                {PERIOD_PRESETS.map(p => (
                    <button
                        key={p.token}
                        type="button"
                        onClick={() => {
                            onChange(presetRange(p.token));
                        }}
                        className={cx(
                            'px-[10px] py-[5px] rounded-full text-11 font-medium select-none',
                            p.token === active
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-3 hover:text-fg-1',
                        )}
                    >
                        {p.label}
                    </button>
                ))}
            </div>

            <div className="flex items-center gap-[6px] text-12 text-fg-3">
                <DateField
                    value={period.from}
                    max={period.to}
                    onChange={from => {
                        onChange({ ...period, from });
                    }}
                    ariaLabel="Period start"
                    wrapperClassName="w-[140px]"
                    className={cx(
                        'rounded-sm border border-border-soft bg-bg-1 px-2 py-[4px] text-12 text-fg-1',
                        active === 'custom' && 'border-brand-primary',
                    )}
                />
                <span>→</span>
                <DateField
                    value={period.to}
                    min={period.from}
                    onChange={to => {
                        onChange({ ...period, to });
                    }}
                    ariaLabel="Period end"
                    wrapperClassName="w-[140px]"
                    className={cx(
                        'rounded-sm border border-border-soft bg-bg-1 px-2 py-[4px] text-12 text-fg-1',
                        active === 'custom' && 'border-brand-primary',
                    )}
                />
            </div>
        </div>
    );
}
