import {
    Cell as AriaCell,
    Column as AriaColumn,
    Row as AriaRow,
    Table as AriaTable,
    TableBody as AriaTableBody,
    TableHeader as AriaTableHeader,
    type CellProps,
    type ColumnProps,
    Collection,
    type RowProps,
    type TableBodyProps,
    type TableHeaderProps,
    type TableProps,
    useTableOptions,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';
import { CollectionSelectionCheckbox } from './collectionSelection';

/**
 * Scalar-cell table built on React Aria's `Table` collection (ADR-0035). Rows of
 * values you scan/compare/sort down a column, with a column header. Selection,
 * select-all, and keyboard/range navigation come from RAC; styling reuses the
 * existing `@theme` tokens so it matches the previous native `<table>` chrome.
 */
export function Table(props: TableProps & { children?: React.ReactNode }) {
    return (
        <AriaTable
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'w-full text-sm border-collapse',
            )}
        />
    );
}

export function TableHeader<T extends object>(props: TableHeaderProps<T>) {
    const { selectionBehavior, selectionMode, allowsDragging } = useTableOptions();
    return (
        <AriaTableHeader
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'text-left text-xs text-fg-3 uppercase tracking-wider',
            )}
        >
            {allowsDragging && <AriaColumn className="w-0 border-b border-border-soft" />}
            {selectionBehavior === 'toggle' && (
                <AriaColumn className="w-9 py-2 pl-3 pr-3 align-middle border-b border-border-soft">
                    {selectionMode === 'multiple' && <CollectionSelectionCheckbox />}
                </AriaColumn>
            )}
            <Collection items={props.columns}>{props.children}</Collection>
        </AriaTableHeader>
    );
}

export function Column(props: ColumnProps & { children?: React.ReactNode }) {
    return (
        <AriaColumn
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'py-2 pr-3 first:pl-3 last:pr-3 font-medium outline-none cursor-default border-b border-border-soft',
            )}
        >
            {props.children}
        </AriaColumn>
    );
}

export function TableBody<T extends object>(props: TableBodyProps<T>) {
    return <AriaTableBody {...props} />;
}

export function Row<T extends object>({
    children,
    columns,
    ...props
}: RowProps<T> & { children?: React.ReactNode }) {
    const { selectionBehavior } = useTableOptions();
    return (
        <AriaRow
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'group border-b border-border-soft last:border-b-0 outline-none ' +
                    'data-[hovered]:bg-surface-2 data-[selected]:bg-brand-primary-soft ' +
                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-inset data-[focus-visible]:ring-brand-primary ' +
                    (props.href ? 'cursor-pointer ' : ''),
            )}
        >
            {selectionBehavior === 'toggle' && (
                <AriaCell className="w-9 py-[10px] pl-3 pr-3 align-middle">
                    <CollectionSelectionCheckbox />
                </AriaCell>
            )}
            <Collection items={columns}>{children}</Collection>
        </AriaRow>
    );
}

export function Cell(props: CellProps & { children?: React.ReactNode }) {
    return (
        <AriaCell
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'py-[10px] pr-3 first:pl-3 last:pr-3 align-middle outline-none',
            )}
        />
    );
}
