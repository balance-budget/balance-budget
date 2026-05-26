/*
 * Pure state helpers for the Bulk Inbox draft buffer (issue #84). Keeps the
 * per-row prefill / dirty / build-request / save-all orchestration out of the
 * React component so the orchestration contract (preflight new-CP dedup,
 * sequential per-row commit, best-effort failure handling) is unit-testable.
 *
 * Per-row scope only — the inline editor mirrors the existing detail page's
 * single-line "credit/debit the full BT amount" projection. Splits, custom
 * date, and custom description are still the detail page's job.
 */

import type { Account } from '../api/accounts';
import type { BankAccount } from '../api/bankAccounts';
import type { BankTransaction } from '../api/bankTransactions';
import type { SuggestedCounterAccount } from '../api/counterparties';
import type { components } from '../lib/api-types';
import type { AccountId, BankTransactionId, CounterpartyId } from '../lib/domain';

type WireCategorizeRequest = components['schemas']['CategorizeBankTransactionRequest'];

export type RowDraftMode = 'existing' | 'new';

export type RowDraft = {
    counterpartyMode: RowDraftMode;
    counterpartyId: CounterpartyId | null;
    newCounterpartyName: string;
    accountId: AccountId | null;
};

export type RowStatus =
    | 'ready' // both cells set (or self-transfer + account)
    | 'invalid' // partially filled in a way that won't pass server validation
    | 'empty'; // nothing usefully filled

export function emptyDraft(): RowDraft {
    return {
        counterpartyMode: 'existing',
        counterpartyId: null,
        newCounterpartyName: '',
        accountId: null,
    };
}

export function isPristine(draft: RowDraft, prefill: RowDraft): boolean {
    return (
        draft.counterpartyMode === prefill.counterpartyMode &&
        draft.counterpartyId === prefill.counterpartyId &&
        draft.newCounterpartyName.trim() === prefill.newCounterpartyName.trim() &&
        draft.accountId === prefill.accountId
    );
}

/** IBAN-exact match against known BankAccount records. Mirrors the detail
 *  page's `resolveCounterpartyByIban`. Returns null when no link is known. */
export function resolveCounterpartyByIban(
    iban: string | null,
    bankAccounts: readonly BankAccount[],
): CounterpartyId | null {
    if (!iban) return null;
    const normalised = iban.replace(/\s+/g, '').toUpperCase();
    for (const ba of bankAccounts) {
        if (!ba.iban) continue;
        if (ba.iban.replace(/\s+/g, '').toUpperCase() !== normalised) continue;
        if (ba.counterpartyId !== null) return ba.counterpartyId;
    }
    return null;
}

/** Build the initial server-derived prefill for a BT row: counterparty from
 *  IBAN match (if any), no account yet. The account is filled async once the
 *  per-counterparty suggestion query resolves — see `applyAccountSuggestion`. */
export function initialPrefill(bt: BankTransaction, bankAccounts: readonly BankAccount[]): RowDraft {
    return {
        counterpartyMode: 'existing',
        counterpartyId: resolveCounterpartyByIban(bt.counterpartyAccountNumber, bankAccounts),
        newCounterpartyName: '',
        accountId: null,
    };
}

/** Pick the best suggested counter-side account for a single-line bulk row,
 *  honouring the per-row currency filter and excluding the BT's own bank-side
 *  account. Returns null when no suggestion fits. */
export function pickSuggestedAccountId(
    suggestions: readonly SuggestedCounterAccount[],
    accountsById: ReadonlyMap<AccountId, Account>,
    currencyCode: string,
    ownBankSideAccountId: AccountId | null,
): AccountId | null {
    for (const s of suggestions) {
        if (s.accountId === ownBankSideAccountId) continue;
        const account = accountsById.get(s.accountId);
        if (!account) continue;
        if (account.currencyCode !== currencyCode) continue;
        return s.accountId;
    }
    return null;
}

/** Update a prefill with the resolved suggested account. The bulk editor uses
 *  a single counter-line for the full BT amount, so we collapse the suggested
 *  set down to a single pick — splits land on the detail page. */
export function withSuggestedAccount(prefill: RowDraft, accountId: AccountId | null): RowDraft {
    if (accountId === null) return prefill;
    if (prefill.accountId !== null) return prefill;
    return { ...prefill, accountId };
}

export function rowStatus(draft: RowDraft): RowStatus {
    const hasAccount = draft.accountId !== null;
    const hasNewName =
        draft.counterpartyMode === 'new' && draft.newCounterpartyName.trim().length > 0;
    const hasExistingCp =
        draft.counterpartyMode === 'existing' && draft.counterpartyId !== null;
    // Self-transfer: existing mode with null counterpartyId is legal (CONTEXT.md,
    // ADR 0014(e)) — counts as "counterparty side resolved" as long as the
    // user has explicitly set an account.
    const isSelfTransfer = draft.counterpartyMode === 'existing' && draft.counterpartyId === null;

    if (hasAccount && (hasExistingCp || hasNewName || isSelfTransfer)) return 'ready';
    if (!hasAccount && !hasExistingCp && !hasNewName) return 'empty';
    return 'invalid';
}

/** Distinct (case-insensitive) new-counterparty names across all dirty rows.
 *  Used by the Save-all preflight to create each new CP once before any
 *  categorize call — required because parallel category calls that share an
 *  unseen-CP name would race on the server's name uniqueness. */
export function collectNewCounterpartyNames(
    rows: readonly { draft: RowDraft }[],
): string[] {
    const seen = new Set<string>();
    const out: string[] = [];
    for (const row of rows) {
        if (row.draft.counterpartyMode !== 'new') continue;
        const trimmed = row.draft.newCounterpartyName.trim();
        if (trimmed.length === 0) continue;
        const key = trimmed.toLowerCase();
        if (seen.has(key)) continue;
        seen.add(key);
        out.push(trimmed);
    }
    return out;
}

/** Project a ready row into the `POST /api/bank-transactions/{id}/categorize`
 *  request body. Mirrors the detail page's projection for the single-line
 *  case: one counter-line carrying the full BT magnitude with the inverse
 *  sign, Date = BookingDate, Description = BT.Description, empty line desc.
 *
 *  For 'new' counterparty rows, the caller passes in the
 *  `createdCounterpartiesByName` map populated by the preflight pass, so the
 *  row is committed as an existing-CP categorise. */
export function buildRowRequest(
    bt: BankTransaction,
    draft: RowDraft,
    createdCounterpartiesByName: ReadonlyMap<string, CounterpartyId>,
): WireCategorizeRequest | null {
    if (rowStatus(draft) !== 'ready') return null;
    if (draft.accountId === null) return null;

    let counterpartyId: CounterpartyId | null;
    if (draft.counterpartyMode === 'new') {
        const resolved = createdCounterpartiesByName.get(draft.newCounterpartyName.trim().toLowerCase());
        if (!resolved) return null;
        counterpartyId = resolved;
    } else {
        counterpartyId = draft.counterpartyId;
    }

    const counterSign = bt.money.amount < 0 ? 1 : -1;
    const magnitude = Math.abs(bt.money.amount);

    return {
        counterpartyId,
        newCounterparty: null,
        date: bt.bookingDate,
        description: bt.description.trim() === '' ? null : bt.description,
        lines: [
            {
                accountId: draft.accountId,
                amount: counterSign * magnitude,
                description: null,
            },
        ],
    };
}

export type SaveAllRow = {
    id: BankTransactionId;
    bt: BankTransaction;
    draft: RowDraft;
};

export type SaveAllOutcome =
    | { ok: true }
    | { ok: false; error: string };

export type SaveAllDeps = {
    createCounterparty: (name: string) => Promise<CounterpartyId>;
    categorize: (id: BankTransactionId, request: WireCategorizeRequest) => Promise<void>;
    onProgress?: (done: number, total: number) => void;
    onCounterpartyCreated?: (name: string, id: CounterpartyId) => void;
    onRowResult?: (id: BankTransactionId, outcome: SaveAllOutcome) => void;
};

export type SaveAllSummary = {
    saved: number;
    failed: number;
};

/**
 * Sequential, best-effort Save-all orchestrator.
 *
 * 1. Pre-flight: collect distinct new-counterparty names from `rows` and
 *    `POST /api/counterparties` for each. This avoids the duplicate-name race
 *    when several rows share the same new CP. A failed creation marks every
 *    row that depends on that name as failed; the remaining rows still run.
 *
 * 2. Per row, *sequentially*: call `categorize`. Sequential rather than
 *    parallel is required because the categorise service inserts a
 *    counterparty-side BankAccount keyed on `CounterpartyAccountNumber` and
 *    multiple rows sharing the same IBAN would race the `UNIQUE(Iban)` index,
 *    producing `ConflictError` on all but the first.
 *
 * Failure of any individual row does not abort the remainder.
 */
export async function runSaveAll(
    rows: readonly SaveAllRow[],
    deps: SaveAllDeps,
): Promise<SaveAllSummary> {
    const names = collectNewCounterpartyNames(rows);
    const created = new Map<string, CounterpartyId>();
    const failedNames = new Map<string, string>();
    for (const name of names) {
        try {
            const id = await deps.createCounterparty(name);
            created.set(name.toLowerCase(), id);
            deps.onCounterpartyCreated?.(name, id);
        } catch (err) {
            failedNames.set(name.toLowerCase(), errorMessage(err));
        }
    }

    let saved = 0;
    let failed = 0;
    const total = rows.length;
    deps.onProgress?.(0, total);

    for (const row of rows) {
        const status = rowStatus(row.draft);
        if (status !== 'ready') {
            failed += 1;
            deps.onRowResult?.(row.id, { ok: false, error: 'Row is not ready' });
            deps.onProgress?.(saved + failed, total);
            continue;
        }

        if (row.draft.counterpartyMode === 'new') {
            const key = row.draft.newCounterpartyName.trim().toLowerCase();
            const cpError = failedNames.get(key);
            if (cpError !== undefined) {
                failed += 1;
                deps.onRowResult?.(row.id, { ok: false, error: cpError });
                deps.onProgress?.(saved + failed, total);
                continue;
            }
        }

        const request = buildRowRequest(row.bt, row.draft, created);
        if (request === null) {
            failed += 1;
            deps.onRowResult?.(row.id, { ok: false, error: 'Row is not ready' });
            deps.onProgress?.(saved + failed, total);
            continue;
        }
        try {
            await deps.categorize(row.id, request);
            saved += 1;
            deps.onRowResult?.(row.id, { ok: true });
        } catch (err) {
            failed += 1;
            deps.onRowResult?.(row.id, { ok: false, error: errorMessage(err) });
        }
        deps.onProgress?.(saved + failed, total);
    }

    return { saved, failed };
}

function errorMessage(err: unknown): string {
    if (err instanceof Error) return err.message;
    return 'Failed';
}

// ─────────────────────────────────────────────────────────────────────────────
// Multi-select + bulk-apply (issue #85). Selection state and the bulk-apply
// mutator are pure helpers so the component owns only React wiring.
// ─────────────────────────────────────────────────────────────────────────────

/** Flip a single row's membership in the selection set. */
export function toggleSelection(
    current: ReadonlySet<BankTransactionId>,
    id: BankTransactionId,
): Set<BankTransactionId> {
    const next = new Set(current);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    return next;
}

/**
 * Shift-click range selection. The range from `anchorId` to `targetId`
 * (inclusive, in `orderedIds` order) is set to the *new* state of `targetId`,
 * i.e. the toggled state of the target.
 *
 * - If target was unselected, the range becomes selected (target included).
 * - If target was selected, the range becomes unselected (target included).
 *
 * When anchor or target is not in `orderedIds` (e.g. anchor scrolled off-page),
 * falls back to a plain toggle of the target.
 */
export function computeRangeSelection(
    orderedIds: readonly BankTransactionId[],
    current: ReadonlySet<BankTransactionId>,
    anchorId: BankTransactionId,
    targetId: BankTransactionId,
): Set<BankTransactionId> {
    const anchorIdx = orderedIds.indexOf(anchorId);
    const targetIdx = orderedIds.indexOf(targetId);
    if (anchorIdx === -1 || targetIdx === -1) {
        return toggleSelection(current, targetId);
    }
    const newState = !current.has(targetId);
    const [lo, hi] =
        anchorIdx <= targetIdx ? [anchorIdx, targetIdx] : [targetIdx, anchorIdx];
    const next = new Set(current);
    for (let i = lo; i <= hi; i += 1) {
        const id = orderedIds[i];
        if (id === undefined) continue;
        if (newState) next.add(id);
        else next.delete(id);
    }
    return next;
}

/** Add every id in `visibleIds` to the selection. */
export function selectAllVisible(
    current: ReadonlySet<BankTransactionId>,
    visibleIds: readonly BankTransactionId[],
): Set<BankTransactionId> {
    const next = new Set(current);
    for (const id of visibleIds) next.add(id);
    return next;
}

/** Remove every id in `visibleIds` from the selection. */
export function clearVisibleSelection(
    current: ReadonlySet<BankTransactionId>,
    visibleIds: readonly BankTransactionId[],
): Set<BankTransactionId> {
    const next = new Set(current);
    for (const id of visibleIds) next.delete(id);
    return next;
}

/** "Select-all checkbox" state for a visible row list. */
export type AllVisibleSelectionState = 'all' | 'some' | 'none';

export function allVisibleSelectionState(
    selection: ReadonlySet<BankTransactionId>,
    visibleIds: readonly BankTransactionId[],
): AllVisibleSelectionState {
    if (visibleIds.length === 0) return 'none';
    let selectedCount = 0;
    for (const id of visibleIds) {
        if (selection.has(id)) selectedCount += 1;
    }
    if (selectedCount === 0) return 'none';
    if (selectedCount === visibleIds.length) return 'all';
    return 'some';
}

/** Distinct currency codes across the selected rows, preserving first-seen
 *  order. Used by the sticky footer to decide whether the Account picker is
 *  applyable (single-currency only). */
export function distinctSelectedCurrencies(
    selection: ReadonlySet<BankTransactionId>,
    rows: readonly { id: BankTransactionId; bt: { money: { currencyCode: string } } }[],
): string[] {
    const seen = new Set<string>();
    const out: string[] = [];
    for (const row of rows) {
        if (!selection.has(row.id)) continue;
        const code = row.bt.money.currencyCode;
        if (seen.has(code)) continue;
        seen.add(code);
        out.push(code);
    }
    return out;
}

export type BulkApplyCounterparty =
    | { kind: 'existing'; counterpartyId: CounterpartyId | null }
    | { kind: 'new'; name: string };

export type BulkApplyInput = {
    /** null = don't touch the row's counterparty draft. */
    counterparty: BulkApplyCounterparty | null;
    /** null = don't touch the row's account draft. */
    accountId: AccountId | null;
};

/**
 * Build the new per-row override produced by applying the footer's bulk
 * patch on top of the row's existing override. Mirrors the row picker's
 * intent: Counterparty selection overwrites both `counterpartyMode` and
 * `counterpartyId`/`newCounterpartyName`; Account selection overwrites
 * `accountId`. Fields whose picker is unfilled (`null`) are left alone.
 *
 * Note: this is the *override layer*. The effective draft is still
 * `{ ...prefill, ...override }`, so applying only a counterparty to a row
 * whose effective account was empty still benefits from the suggestion
 * cascade — the override now contains the new cpId, the per-row suggestion
 * query refires, and `withSuggestedAccount` fills the prefill.
 */
export function applyBulkPatchToOverride(
    override: Partial<RowDraft> | undefined,
    input: BulkApplyInput,
): Partial<RowDraft> {
    const next: Partial<RowDraft> = { ...(override ?? {}) };
    if (input.counterparty !== null) {
        if (input.counterparty.kind === 'new') {
            next.counterpartyMode = 'new';
            next.counterpartyId = null;
            next.newCounterpartyName = input.counterparty.name;
        } else {
            next.counterpartyMode = 'existing';
            next.counterpartyId = input.counterparty.counterpartyId;
            next.newCounterpartyName = '';
        }
    }
    if (input.accountId !== null) {
        next.accountId = input.accountId;
    }
    return next;
}
