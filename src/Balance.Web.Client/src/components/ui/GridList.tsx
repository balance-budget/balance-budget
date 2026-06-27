import {
    GridList as AriaGridList,
    GridListItem as AriaGridListItem,
    type GridListItemProps,
    type GridListProps,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

/**
 * List of composite / multi-control / inline-editable rows built on React Aria's
 * `GridList` collection (ADR-0035). The row is the primary focus stop; controls
 * inside a row are secondary tab stops within the focused row. Selection,
 * select-all, and shift/keyboard range selection come from RAC when a
 * `selectionMode` is set; styling reuses the existing `@theme` tokens.
 */
export function GridList<T extends object>(props: GridListProps<T>) {
    return (
        <AriaGridList
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'flex flex-col gap-2 outline-none',
            )}
        />
    );
}

export function GridListItem({ className, ...props }: GridListItemProps) {
    const textValue =
        props.textValue ?? (typeof props.children === 'string' ? props.children : undefined);
    return (
        <AriaGridListItem
            {...props}
            textValue={textValue}
            className={composeTailwindRenderProps(
                className,
                'group rounded-lg border border-border-soft bg-surface-2 outline-none ' +
                    'data-[hovered]:border-border-strong data-[selected]:border-brand-primary data-[selected]:bg-brand-primary-soft ' +
                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-inset data-[focus-visible]:ring-brand-primary',
            )}
        />
    );
}
