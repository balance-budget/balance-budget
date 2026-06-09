import { useLingui } from '@lingui/react/macro';
import { detectPreset, PERIOD_PRESETS, presetRange, type ReportPeriod } from '../lib/reportPeriod';
import { DateRangePicker } from './ui/DateRangePicker';
import { selectedKey } from './ui/selection';
import { ToggleButton, ToggleButtonGroup } from './ui/ToggleButtonGroup';

type PeriodPickerProps = {
    period: ReportPeriod;
    onChange: (period: ReportPeriod) => void;
};

/**
 * Reporting-period control: preset pills plus a custom date-range picker.
 * Presets just compute an [from, to] range and hand it up; the active pill is
 * derived by matching the current period against each preset, so a custom range
 * (or a shared URL) lights up none of them.
 */
export function PeriodPicker({ period, onChange }: PeriodPickerProps) {
    const { t } = useLingui();
    const active = detectPreset(period);

    return (
        <div className="flex flex-wrap items-center gap-2">
            <ToggleButtonGroup
                aria-label={t`Period presets`}
                selectedKeys={active === 'custom' ? [] : [active]}
                onSelectionChange={keys => {
                    const token = selectedKey(keys);
                    const preset = PERIOD_PRESETS.find(p => p.token === token);
                    if (preset) onChange(presetRange(preset.token));
                }}
            >
                {PERIOD_PRESETS.map(p => (
                    <ToggleButton key={p.token} id={p.token}>
                        {p.label}
                    </ToggleButton>
                ))}
            </ToggleButtonGroup>

            <DateRangePicker
                aria-label={t`Reporting period`}
                value={{ from: period.from, to: period.to }}
                onChange={range => {
                    if (range.from !== '' && range.to !== '') {
                        onChange({ from: range.from, to: range.to });
                    }
                }}
                fieldClassName={
                    active === 'custom'
                        ? 'text-xs py-[4px] border-brand-primary'
                        : 'text-xs py-[4px]'
                }
            />
        </div>
    );
}
