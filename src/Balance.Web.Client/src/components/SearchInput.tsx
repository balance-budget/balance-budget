import { Icon } from './Icon';

type SearchInputProps = {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
    ariaLabel?: string;
};

export function SearchInput({ value, onChange, placeholder, ariaLabel }: SearchInputProps) {
    return (
        <div className="relative">
            <span
                className="absolute inset-y-0 left-2 flex items-center text-fg-3 pointer-events-none"
                aria-hidden="true"
            >
                <Icon name="search" size={14} strokeWidth={1.75} />
            </span>
            <input
                type="search"
                value={value}
                onChange={e => {
                    onChange(e.target.value);
                }}
                placeholder={placeholder ?? 'Search…'}
                aria-label={ariaLabel ?? placeholder ?? 'Search'}
                className="w-full pl-7 pr-2 py-1.5 rounded-sm bg-surface-2 text-[13px] text-fg-1 placeholder:text-fg-3 outline-none focus:ring-1 focus:ring-brand-primary"
            />
        </div>
    );
}
