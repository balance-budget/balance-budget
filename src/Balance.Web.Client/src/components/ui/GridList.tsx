import {
    GridList as AriaGridList,
    GridListItem as AriaGridListItem,
    type GridListItemProps,
    type GridListProps,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

/**
 * Visual variant for the list:
 * - `card` (default): spaced, self-contained rows (record lists like Tokens).
 * - `flat`: dense rows separated by a bottom border, no card chrome and no gap
 *   (the bank-transaction inbox, which reads as a table-like grid).
 */
export type GridListVariant = 'card' | 'flat';

/**
 * List of composite / multi-control / inline-editable rows built on React Aria's
 * `GridList` collection (ADR-0035). The row is the primary focus stop; controls
 * inside a row are secondary tab stops within the focused row. Selection,
 * select-all, and shift/keyboard range selection come from RAC when a
 * `selectionMode` is set; styling reuses the existing `@theme` tokens.
 */
export function GridList<T extends object>({
    variant = 'card',
    ...props
}: GridListProps<T> & { variant?: GridListVariant }) {
    return (
        <AriaGridList
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                variant === 'card'
                    ? 'flex flex-col gap-2 outline-none'
                    : 'flex flex-col outline-none',
            )}
        />
    );
}

const ITEM_CHROME: Record<GridListVariant, string> = {
    card:
        'group rounded-lg border border-border-soft bg-surface-2 outline-none ' +
        'data-[hovered]:border-border-strong data-[selected]:border-brand-primary data-[selected]:bg-brand-primary-soft ' +
        'data-[focus-visible]:ring-1 data-[focus-visible]:ring-inset data-[focus-visible]:ring-brand-primary',
    flat:
        'group border-b border-border-soft last:border-b-0 outline-none ' +
        'data-[hovered]:bg-surface-2 data-[selected]:bg-brand-primary-soft ' +
        'data-[focus-visible]:ring-1 data-[focus-visible]:ring-inset data-[focus-visible]:ring-brand-primary',
};

export function GridListItem({
    className,
    variant = 'card',
    ...props
}: GridListItemProps & { variant?: GridListVariant }) {
    const textValue =
        props.textValue ?? (typeof props.children === 'string' ? props.children : undefined);
    return (
        <AriaGridListItem
            {...props}
            textValue={textValue}
            className={composeTailwindRenderProps(className, ITEM_CHROME[variant])}
        />
    );
}
