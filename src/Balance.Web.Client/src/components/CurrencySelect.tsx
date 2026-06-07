import { useCurrencies } from '../api/currencies';
import { Select, SelectItem } from './ui/Select';

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
        <div className="flex items-center gap-2 text-xs text-fg-3">
            <span id="currency-select-label">Currency</span>
            <Select
                aria-labelledby="currency-select-label"
                value={value}
                onChange={key => {
                    if (key !== null) onChange(String(key));
                }}
            >
                {options.map(code => (
                    <SelectItem key={code} id={code}>
                        {currencies.data?.get(code)?.name ?? code}
                    </SelectItem>
                ))}
            </Select>
        </div>
    );
}
