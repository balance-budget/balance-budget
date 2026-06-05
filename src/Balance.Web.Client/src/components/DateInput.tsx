/** A `yyyy-MM-dd` date filter input styled like the other toolbar inputs; '' = unset. */
export function DateInput({
    value,
    onChange,
    ariaLabel,
    min,
    max,
}: {
    value: string;
    onChange: (value: string) => void;
    ariaLabel: string;
    min?: string;
    max?: string;
}) {
    return (
        <input
            type="date"
            value={value}
            min={min}
            max={max}
            onChange={e => {
                onChange(e.target.value);
            }}
            aria-label={ariaLabel}
            className="px-3 py-[7px] rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-13 focus:outline-none focus:border-border-strong"
        />
    );
}
