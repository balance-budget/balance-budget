import { useCurrencies } from '../api/currencies';

type CurrencySelectProps = {
    value: string;
    onChange: (currencyCode: string) => void;
};

/**
 * Currency selector for the Insights page. Reports are scoped to one currency
 * at a time (no FX conversion in v1), so this picks which currency's accounts
 * the Distribution and Money flow are drawn from. Options come from the backend
 * currency catalog; the current value is always offered even if the catalog
 * hasn't resolved yet, so first paint is stable.
 */
export function CurrencySelect({ value, onChange }: CurrencySelectProps) {
    const currencies = useCurrencies();
    const codes = currencies.data ? [...currencies.data.keys()] : [];
    const options = codes.includes(value) ? codes : [value, ...codes];

    return (
        <label className="flex items-center gap-2 text-12 text-fg-3">
            <span>Currency</span>
            <select
                value={value}
                onChange={e => {
                    onChange(e.target.value);
                }}
                className="rounded-sm border border-border-soft bg-bg-1 px-2 py-[4px] text-fg-1"
            >
                {options.map(code => (
                    <option key={code} value={code}>
                        {currencies.data?.get(code)?.name ?? code}
                    </option>
                ))}
            </select>
        </label>
    );
}
