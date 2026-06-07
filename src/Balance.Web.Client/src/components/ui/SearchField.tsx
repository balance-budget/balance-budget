import { Search, X } from 'lucide-react';
import {
    Button,
    Input,
    SearchField as AriaSearchField,
    type SearchFieldProps as AriaSearchFieldProps,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

export type SearchFieldProps = AriaSearchFieldProps & {
    placeholder?: string;
};

/** Compact toolbar search box — icon prefix, clear button, brand focus ring. */
export function SearchField({ placeholder, ...props }: SearchFieldProps) {
    return (
        <AriaSearchField
            {...props}
            className={composeTailwindRenderProps(props.className, 'group relative')}
        >
            <span
                className="absolute inset-y-0 left-2 flex items-center text-fg-3 pointer-events-none"
                aria-hidden="true"
            >
                <Search size={14} strokeWidth={1.75} />
            </span>
            <Input
                placeholder={placeholder ?? 'Search…'}
                className={
                    'w-full pl-7 pr-7 py-1.5 rounded-sm bg-surface-2 text-13 text-fg-1 ' +
                    'placeholder:text-fg-3 outline-none focus:ring-1 focus:ring-brand-primary ' +
                    '[&::-webkit-search-cancel-button]:appearance-none'
                }
            />
            <Button className="absolute inset-y-0 right-1 flex items-center px-1 text-fg-3 data-[hovered]:text-fg-1 outline-none group-data-[empty]:invisible">
                <X size={13} strokeWidth={2} aria-hidden="true" />
            </Button>
        </AriaSearchField>
    );
}
