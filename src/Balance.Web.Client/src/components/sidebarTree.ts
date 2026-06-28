import type { Account } from '../api/accounts';
import { buildChildrenMap, groupRootsByType } from '../lib/accountTree';
import type { AccountType } from '../lib/domain';

/** Where each visible row sits in its nesting block, so the per-row shade can be
 *  rounded at the block's first/last row (RAC's Tree DOM is flat sibling rows,
 *  so the old rounded nested-container look is reconstructed per row). */
export type RowDecor = { roundTop: boolean; roundBottom: boolean };

/**
 * Walk the visible tree in render order (per type section, then DFS through
 * expanded nodes) and mark, for each row, whether it opens or closes a nesting
 * block. A block opens at a node's first child (the level steps up) and closes
 * at its last visible descendant (the next row steps back to a shallower level).
 */
export function computeRowDecor(
    accounts: readonly Account[],
    typeOrder: readonly AccountType[],
    expanded: ReadonlySet<string>,
): Map<string, RowDecor> {
    const childrenByParent = buildChildrenMap(accounts);
    const rootsByType = groupRootsByType(accounts);
    const flat: { id: string; level: number }[] = [];
    const visit = (account: Account, level: number) => {
        flat.push({ id: String(account.id), level });
        if (expanded.has(String(account.id))) {
            for (const child of childrenByParent.get(account.id) ?? []) visit(child, level + 1);
        }
    };
    for (const type of typeOrder) {
        for (const root of rootsByType.get(type) ?? []) visit(root, 1);
    }

    const decor = new Map<string, RowDecor>();
    for (let i = 0; i < flat.length; i += 1) {
        const cur = flat[i];
        if (!cur) continue;
        const prev = flat[i - 1];
        const next = flat[i + 1];
        decor.set(cur.id, {
            roundTop: cur.level > 1 && (!prev || prev.level < cur.level),
            roundBottom: cur.level > 1 && (!next || next.level < cur.level),
        });
    }
    return decor;
}
