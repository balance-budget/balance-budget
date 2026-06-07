import { Search, X } from 'lucide-react';
import {
    Button,
    Input,
    SearchField as AriaSearchField,
    type SearchFieldProps as AriaSearchFieldProps,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';
import { groupStyles } from './styles';

export type SearchFieldProps = AriaSearchFieldProps & {
    placeholder?: string;
};

/** Toolbar search box — standard field chrome with an icon prefix and clear button. */
export function SearchField({ placeholder, ...props }: SearchFieldProps) {
    return (
        <AriaSearchField
            {...props}
            className={composeTailwindRenderProps(props.className, 'group')}
        >
            <div className={cx(groupStyles, 'px-3')}>
                <span
                    className="flex items-center text-fg-3 pointer-events-none"
                    aria-hidden="true"
                >
                    <Search size={14} strokeWidth={1.75} />
                </span>
                <Input
                    placeholder={placeholder ?? 'Search…'}
                    className={
                        'h-full flex-1 min-w-0 px-2 bg-transparent outline-none ' +
                        '[&::-webkit-search-cancel-button]:appearance-none'
                    }
                />
                <Button className="flex items-center text-fg-3 data-[hovered]:text-fg-1 outline-none group-data-[empty]:invisible">
                    <X size={13} strokeWidth={2} aria-hidden="true" />
                </Button>
            </div>
        </AriaSearchField>
    );
}
