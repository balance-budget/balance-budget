import {
    Button,
    Tree as AriaTree,
    TreeItem as AriaTreeItem,
    TreeItemContent as AriaTreeItemContent,
    type TreeItemContentRenderProps,
    type TreeItemProps,
    type TreeProps,
} from 'react-aria-components';
import { Icon } from '../Icon';
import { composeTailwindRenderProps } from './compose';

/**
 * Chart-of-accounts hierarchy built on React Aria's `Tree` collection
 * (ADR-0035): `role="treeitem"`, `aria-level`, set-size, and arrow-key
 * expand/collapse come from RAC. Type grouping stays outside the tree (one
 * `<Tree>` per type section) because RAC `Tree` has no `Section`.
 */
export function Tree<T extends object>(props: TreeProps<T>) {
    return (
        <AriaTree
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'flex flex-col gap-[2px] outline-none',
            )}
        />
    );
}

export function TreeItem(props: TreeItemProps) {
    return (
        <AriaTreeItem
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'outline-none rounded-lg ' +
                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-inset data-[focus-visible]:ring-brand-primary',
            )}
        />
    );
}

/** Built-in chevron expand/collapse button via RAC's `chevron` slot. */
export function TreeExpandButton({
    ariaLabel,
    isExpanded,
}: {
    ariaLabel: string;
    isExpanded: boolean;
}) {
    return (
        <Button
            slot="chevron"
            aria-label={ariaLabel}
            className="shrink-0 p-1 rounded-lg text-fg-3 cursor-pointer outline-none data-[hovered]:text-fg-1 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary"
        >
            <Icon
                name="chevron-right"
                size={14}
                className={'transition-transform duration-120 ' + (isExpanded ? 'rotate-90' : '')}
            />
        </Button>
    );
}

export { AriaTreeItemContent as TreeItemContent };
export type { TreeItemContentRenderProps };
