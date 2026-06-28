import { type ReactNode } from 'react';
import { useLingui } from '@lingui/react/macro';
import { type MessageDescriptor } from '@lingui/core';
import { Collection, type Key, type Selection } from 'react-aria-components';
import { type Account } from '../api/accounts';
import { buildChildrenMap, groupRootsByType } from '../lib/accountTree';
import { type AccountId, type AccountType } from '../lib/domain';
import { Tree, TreeItem, TreeItemContent } from './ui/Tree';

/** State passed to a row renderer for one account node. */
export type AccountRowContext = {
    hasChildren: boolean;
    isExpanded: boolean;
    /** RAC nesting depth, starting at 1 for a root. */
    level: number;
    /** First in its sibling group — rounds the top of its band (ADR-0036). */
    isFirstChild: boolean;
    /** Last in its sibling group — closes the bottom of its band when collapsed/leaf. */
    isLastChild: boolean;
    /** Whether this row's parent is itself the last child of the grandparent.
     *  Drives whether the parent band also closes here (a doubly-closed bottom). */
    parentIsLastChild: boolean;
};

type AccountTreeSectionsProps = {
    accounts: readonly Account[];
    /** Type sections to render, in order. The sidebar omits Equity. */
    typeOrder: readonly AccountType[];
    typeLabels: Record<AccountType, MessageDescriptor>;
    renderHeading: (label: string) => ReactNode;
    renderRow: (account: Account, ctx: AccountRowContext) => ReactNode;
    /** Controlled expansion (the sidebar persists + auto-expands ancestors). */
    expandedKeys?: Iterable<Key>;
    onExpandedChange?: (keys: Set<Key>) => void;
    defaultExpandedKeys?: Iterable<Key> | 'all';
    /** Whole-row action (e.g. navigate to the register). */
    onAction?: (key: AccountId) => void;
    /** Per-`<Tree>` (type-section) class, e.g. inter-row gap. */
    treeClassName?: string;
};

/**
 * The chart-of-accounts hierarchy as one RAC `Tree` per type section (ADR-0035).
 * Tree structure and grouping come from the shared `lib/accountTree` helpers;
 * the Accounts screen and the sidebar swap row content via `renderRow`.
 */
export function AccountTreeSections({
    accounts,
    typeOrder,
    typeLabels,
    renderHeading,
    renderRow,
    expandedKeys,
    onExpandedChange,
    defaultExpandedKeys,
    onAction,
    treeClassName,
}: AccountTreeSectionsProps) {
    const { i18n } = useLingui();
    const childrenByParent = buildChildrenMap(accounts);
    const rootsByType = groupRootsByType(accounts);
    const byId = new Map(accounts.map(a => [a.id, a]));

    function renderItem(account: Account): ReactNode {
        const children = childrenByParent.get(account.id) ?? [];
        const siblings = childrenByParent.get(account.parentId) ?? [];
        const isFirstChild = siblings[0]?.id === account.id;
        const isLastChild = siblings[siblings.length - 1]?.id === account.id;
        const parent = account.parentId !== null ? byId.get(account.parentId) : undefined;
        const parentSiblings = parent ? (childrenByParent.get(parent.parentId) ?? []) : [];
        const parentIsLastChild =
            parent !== undefined && parentSiblings[parentSiblings.length - 1]?.id === parent.id;
        return (
            <TreeItem id={account.id} textValue={account.name}>
                <TreeItemContent>
                    {({ level, hasChildItems, isExpanded }) =>
                        renderRow(account, {
                            hasChildren: hasChildItems,
                            isExpanded,
                            level,
                            isFirstChild,
                            isLastChild,
                            parentIsLastChild,
                        })
                    }
                </TreeItemContent>
                <Collection items={children}>{renderItem}</Collection>
            </TreeItem>
        );
    }

    const selectionProps =
        expandedKeys !== undefined
            ? {
                  expandedKeys,
                  onExpandedChange: (keys: Selection) => {
                      if (onExpandedChange) onExpandedChange(keys === 'all' ? new Set() : keys);
                  },
              }
            : { defaultExpandedKeys };

    return (
        <div className="flex flex-col gap-5">
            {typeOrder.map(type => {
                const roots = rootsByType.get(type);
                if (!roots || roots.length === 0) return null;
                return (
                    <div key={type} className="flex flex-col">
                        {renderHeading(i18n._(typeLabels[type]))}
                        <Tree
                            aria-label={i18n._(typeLabels[type])}
                            className={treeClassName}
                            items={roots}
                            selectionMode="none"
                            {...selectionProps}
                            {...(onAction
                                ? {
                                      onAction: (key: Key) => {
                                          onAction(key as AccountId);
                                      },
                                  }
                                : {})}
                        >
                            {renderItem}
                        </Tree>
                    </div>
                );
            })}
        </div>
    );
}
