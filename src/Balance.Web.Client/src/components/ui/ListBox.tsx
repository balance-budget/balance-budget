import {
    Collection,
    Header,
    ListBox as AriaListBox,
    ListBoxItem,
    type ListBoxItemProps,
    type ListBoxProps,
    ListBoxSection,
    type SectionProps,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

/** Scrolling option list used inside Select/ComboBox popovers. */
export function DropdownListBox<T extends object>(props: ListBoxProps<T>) {
    return (
        <AriaListBox
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'outline-none max-h-64 overflow-y-auto scrollbar-sleek',
            )}
        />
    );
}

/** A single option row — focused rows take the brand highlight (matches the
 *  previous hand-rolled ComboBox's active style). */
export function DropdownItem(props: ListBoxItemProps) {
    return (
        <ListBoxItem
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'px-3 py-2 cursor-pointer flex items-center gap-2 text-sm text-fg-1 outline-none ' +
                    'data-[focused]:bg-brand-primary-soft data-[focused]:text-brand-primary ' +
                    'data-[disabled]:opacity-60',
            )}
        />
    );
}

export type DropdownSectionProps<T> = SectionProps<T> & {
    title: string;
    items?: Iterable<T>;
};

/** Grouped options with the uppercase micro-header from the previous ComboBox. */
export function DropdownSection<T extends object>({
    title,
    items,
    children,
    ...props
}: DropdownSectionProps<T>) {
    return (
        <ListBoxSection {...props}>
            <Header className="px-3 py-1 text-xs text-fg-3 uppercase tracking-wider bg-surface-2 border-b border-border-soft">
                {title}
            </Header>
            <Collection items={items}>{children}</Collection>
        </ListBoxSection>
    );
}
