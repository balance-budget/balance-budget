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
    /** Leading indent applied per nesting level, in rem. */
    indentRem?: number;
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
    indentRem = 1.25,
}: AccountTreeSectionsProps) {
    const { i18n } = useLingui();
    const childrenByParent = buildChildrenMap(accounts);
    const rootsByType = groupRootsByType(accounts);

    function renderItem(account: Account): ReactNode {
        const children = childrenByParent.get(account.id) ?? [];
        return (
            <TreeItem id={account.id} textValue={account.name}>
                <TreeItemContent>
                    {({ level, hasChildItems, isExpanded }) => (
                        <div
                            className="flex items-center"
                            style={{ paddingLeft: `${String((level - 1) * indentRem)}rem` }}
                        >
                            {renderRow(account, { hasChildren: hasChildItems, isExpanded, level })}
                        </div>
                    )}
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
