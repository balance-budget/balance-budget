import type { RegisterRow } from '../api/register';
import type { JournalLineId } from '../lib/domain';

/**
 * Pure selection derivations for the account register (ADR-0035). RAC's
 * `Table` owns the selection *mechanics* (toggle, range, select-all, keyboard);
 * these helpers only decide which rows are selectable and keep the selection
 * page-bound, so they can be unit-tested without rendering.
 *
 * The rule (ADR): only `Uncleared` lines can be moved, so only they are
 * selectable; cleared/reconciled lines are disabled for selection but still
 * navigable.
 */
const isMovable = (row: RegisterRow): boolean => row.reconciliationStatus === 'Uncleared';

/** The line ids on the current page that may be selected (the select-all set). */
export function selectableLineIds(rows: readonly RegisterRow[]): JournalLineId[] {
    return rows.filter(isMovable).map(r => r.journalLineId);
}

/** The line ids on the current page that must be disabled for selection. */
export function disabledLineKeys(rows: readonly RegisterRow[]): Set<JournalLineId> {
    return new Set(rows.filter(r => !isMovable(r)).map(r => r.journalLineId));
}

/**
 * The subset of `selected` still present and movable on the current page.
 * Selection is page-bound and self-pruning: ids that fell off the page (or
 * stopped being movable) drop out.
 */
export function prunePageSelection(
    rows: readonly RegisterRow[],
    selected: ReadonlySet<JournalLineId>,
): Set<JournalLineId> {
    return new Set(
        rows.filter(r => isMovable(r) && selected.has(r.journalLineId)).map(r => r.journalLineId),
    );
}
