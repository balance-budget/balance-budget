import { useMemo, useState } from 'react';
import { useBlocker } from '@tanstack/react-router';
import { type Selection } from 'react-aria-components';
import { t } from '@lingui/core/macro';
import { useLingui } from '@lingui/react/macro';
import { useQueries, useQueryClient } from '@tanstack/react-query';
import { accountsKeys, type Account } from '../api/accounts';
import { bankAccountsKeys, type BankAccount } from '../api/bankAccounts';
import { journalEntriesKeys } from '../api/journalEntries';
import { bankTransactionsKeys, type BankTransaction } from '../api/bankTransactions';
import {
    counterpartiesKeys,
    type Counterparty,
    type SuggestedCounterAccount,
} from '../api/counterparties';
import { type ComboBoxItem } from '../components/ui/combobox.state';
import { useToast } from '../components/ui/Toast';
import {
    asAccountId,
    asCounterpartyId,
    type AccountId,
    type BankTransactionId,
    type CounterpartyId,
} from '../lib/domain';
import { getJson, postJson } from '../lib/http';
import {
    applyBulkPatchToOverride,
    buildSuggestionOverride,
    distinctRowCurrencies,
    emptyDraft,
    isPristine,
    removeKeysFor,
    resolveCounterpartyByIban,
    rowStatus,
    runSaveAll,
    setBulkDismissDrafts,
    type BulkApplyCounterparty,
    type BulkApplyInput,
    type RowDraft,
    type SaveAllOutcome,
    type SaveAllSummary,
} from './bankTransactionsInbox.state';
import type { components } from '../lib/api-types.gen';

type WireCounterparty = components['schemas']['CounterpartyOutput'];
type WireJournalEntry = components['schemas']['JournalEntryOutput'];
type WireCategorizeRequest = components['schemas']['CategorizeBankTransactionRequest'];
type WireSuggested = components['schemas']['SuggestedCounterAccountOutput'];

/**
 * Props for the combined selection-actions + save-controls bar. Picker values
 * (`bulkCounterparty`, `bulkAccountId`) are owned by {@link useInboxEditor} so
 * they survive Apply, Clear, and pagination.
 */
export type ActionBarProps = {
    selectionCount: number;
    selectedCurrencies: readonly string[];
    bulkCounterparty: BulkApplyCounterparty | null;
    bulkAccountId: AccountId | null;
    counterpartyItems: ComboBoxItem<CounterpartyId | null>[];
    /** The one currency shared by the selection, or null when it spans several
     *  (the account picker is disabled in that case). */
    bulkCurrency: string | null;
    /** Bank-side accounts of the selected rows — never bulk-pickable. */
    excludeAccountIds: ReadonlySet<AccountId>;
    saving: boolean;
    progress: { done: number; total: number } | null;
    dirtyCount: number;
    readyCount: number;
    onBulkCounterpartyChange: (cp: BulkApplyCounterparty | null) => void;
    onBulkAccountIdChange: (id: AccountId | null) => void;
    onApply: () => void;
    onApplySuggestions: () => void;
    onBulkDismiss: () => void;
    onClearSelection: () => void;
    onSave: () => void;
    onDiscard: () => void;
};

/** Everything the inbox editor view needs to render — see {@link useInboxEditor}. */
export type InboxEditorModel = {
    actionBarProps: ActionBarProps;
    visibleBts: BankTransaction[];
    visibleIds: BankTransactionId[];
    selection: Set<BankTransactionId>;
    saving: boolean;
    prefillByBt: Map<BankTransactionId, RowDraft>;
    dismissDrafts: Map<BankTransactionId, string>;
    rowErrors: Map<BankTransactionId, string>;
    counterpartyItems: ComboBoxItem<CounterpartyId | null>[];
    bankAccountsById: Map<string, BankAccount>;
    dirtyCount: number;
    selectionCount: number;
    bulkDismissOpen: boolean;
    setBulkDismissOpen: (open: boolean) => void;
    discardOpen: boolean;
    setDiscardOpen: (open: boolean) => void;
    /** Navigation blocker (resolver form) — the render reads `status` and calls
     *  `proceed`/`reset` from the `'blocked'` branch. */
    blocker: { status: 'blocked'; proceed: () => void; reset: () => void } | { status: 'idle' };
    draftFor: (id: BankTransactionId) => RowDraft;
    isRowPristine: (id: BankTransactionId) => boolean;
    patchDraft: (id: BankTransactionId, patch: Partial<RowDraft>) => void;
    resetRow: (id: BankTransactionId) => void;
    /** RAC selection change for the inbox GridList. */
    onSelectionChange: (keys: Selection) => void;
    /** Toggle the page-wide select-all (header checkbox). */
    onToggleSelectAll: () => void;
    applyBulkDismiss: (reason: string) => void;
    discardAll: () => void;
};

/**
 * Owns the whole inbox-editing unit of work: per-row draft overrides, dismiss
 * drafts, row errors, optimistic saved-row hiding, multi-select, the bulk
 * picker buffers, and the Save-all orchestration. The pure transitions live in
 * `bankTransactionsInbox.state.ts`; this hook is the stateful shell that wires
 * them to React Query and the toast/blocker side effects, leaving the screen a
 * thin render over {@link InboxEditorModel}.
 */
export function useInboxEditor({
    bankTransactions,
    accounts,
    counterparties,
    bankAccounts,
}: {
    bankTransactions: BankTransaction[];
    accounts: Account[];
    counterparties: Counterparty[];
    bankAccounts: BankAccount[];
}): InboxEditorModel {
    const { t } = useLingui();
    const toast = useToast();
    const queryClient = useQueryClient();

    const accountsById = useMemo(() => {
        const m = new Map<AccountId, Account>();
        for (const a of accounts) m.set(a.id, a);
        return m;
    }, [accounts]);

    const bankAccountsById = useMemo(() => {
        const m = new Map<string, BankAccount>();
        for (const ba of bankAccounts) m.set(ba.id, ba);
        return m;
    }, [bankAccounts]);

    // ── Per-row state ────────────────────────────────────────────────────────
    // userOverrides: Partial<RowDraft> the user has typed on top of the
    // server-derived prefill. Keeping this separate from the prefill lets the
    // prefill be derived from props (BT + bankAccounts + suggestion query
    // results) without setState-in-effect: re-rendering composes the effective
    // draft as `{ ...prefill, ...overrides.get(id) }`.
    const [userOverrides, setUserOverrides] = useState<Map<BankTransactionId, Partial<RowDraft>>>(
        new Map(),
    );
    // Issue #86: dismiss-draft buffer. Key presence means "this row will be
    // dismissed at Save-all with the stored reason". Mutually exclusive with
    // userOverrides — setting either side clears the other for that row.
    const [dismissDrafts, setDismissDrafts] = useState<Map<BankTransactionId, string>>(new Map());
    const [rowErrors, setRowErrors] = useState<Map<BankTransactionId, string>>(new Map());
    // Optimistically hide rows we just saved — the BT query refetch will
    // exclude them once it lands, but we want them gone immediately so the
    // user sees the inbox shrink as Save-all ticks through.
    const [savedIds, setSavedIds] = useState<Set<BankTransactionId>>(new Set());

    const visibleBts = useMemo(
        () => bankTransactions.filter(bt => !savedIds.has(bt.id)),
        [bankTransactions, savedIds],
    );

    // Inbox-suggestion-gating amendment to ADR 0013: rows render pristine —
    // no IBAN→counterparty pre-fill, no last-used-account pre-fill. The
    // IBAN-resolved cp is still computed here so the suggestion queries can
    // pre-warm the cache for the user's eventual "Apply suggestions" click.
    const ibanResolvedCpByBt = useMemo(() => {
        const m = new Map<BankTransactionId, CounterpartyId | null>();
        for (const bt of visibleBts) {
            m.set(bt.id, resolveCounterpartyByIban(bt.counterpartyAccountNumber, bankAccounts));
        }
        return m;
    }, [visibleBts, bankAccounts]);

    // The cp we fire the suggestion query for, per row: user override wins,
    // otherwise the IBAN-resolved cp. Self-transfer (null) skips the fetch.
    const cpIdByBt = useMemo(() => {
        const m = new Map<BankTransactionId, CounterpartyId | null>();
        for (const bt of visibleBts) {
            const override = userOverrides.get(bt.id);
            let cpId: CounterpartyId | null;
            if (override?.counterpartyMode === 'new') {
                cpId = null;
            } else if (override && 'counterpartyId' in override) {
                cpId = override.counterpartyId ?? null;
            } else {
                cpId = ibanResolvedCpByBt.get(bt.id) ?? null;
            }
            m.set(bt.id, cpId);
        }
        return m;
    }, [visibleBts, userOverrides, ibanResolvedCpByBt]);

    // Dedupe to unique non-null cpIds — multiple rows often share the same
    // counterparty, and useQueries warns "Duplicate Queries found" (and churns
    // observers on every render) if entries share a queryKey.
    const uniqueCpIds = useMemo(() => {
        const set = new Set<CounterpartyId>();
        for (const cpId of cpIdByBt.values()) {
            if (cpId !== null) set.add(cpId);
        }
        return [...set];
    }, [cpIdByBt]);

    const suggestionQueries = useQueries({
        queries: uniqueCpIds.map(cpId => ({
            queryKey: counterpartiesKeys.suggestedAccounts(cpId),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const wire = await getJson<WireSuggested[]>(
                    `/api/counterparties/${cpId}/suggested-accounts`,
                    signal,
                    'load suggested accounts',
                );
                return wire.map(w => ({
                    accountId: asAccountId(w.accountId),
                    amount: typeof w.amount === 'string' ? Number(w.amount) : w.amount,
                }));
            },
        })),
    });

    const suggestionsByCpId = useMemo(() => {
        const m = new Map<CounterpartyId, SuggestedCounterAccount[]>();
        uniqueCpIds.forEach((cpId, i) => {
            const data = suggestionQueries[i]?.data;
            if (data) m.set(cpId, data);
        });
        return m;
    }, [uniqueCpIds, suggestionQueries]);

    const suggestionPendingByCpId = useMemo(() => {
        const m = new Set<CounterpartyId>();
        uniqueCpIds.forEach((cpId, i) => {
            if (suggestionQueries[i]?.isPending) m.add(cpId);
        });
        return m;
    }, [uniqueCpIds, suggestionQueries]);

    // Prefill stays empty for every row — see the gating amendment to ADR
    // 0014. The user's override layer is the only thing that fills the draft;
    // until the user manually picks or clicks Apply suggestions on a
    // selection, the row stays pristine and Save-all leaves it alone.
    const prefillByBt = useMemo(() => {
        const m = new Map<BankTransactionId, RowDraft>();
        for (const bt of visibleBts) {
            m.set(bt.id, emptyDraft());
        }
        return m;
    }, [visibleBts]);

    function draftFor(id: BankTransactionId): RowDraft {
        const prefill = prefillByBt.get(id) ?? emptyDraft();
        const override = userOverrides.get(id);
        return { ...prefill, ...override };
    }

    function isRowPristine(id: BankTransactionId): boolean {
        const override = userOverrides.get(id);
        if (!override) return true;
        const prefill = prefillByBt.get(id);
        if (!prefill) return false;
        return isPristine({ ...prefill, ...override }, prefill);
    }

    function patchDraft(id: BankTransactionId, patch: Partial<RowDraft>) {
        setRowErrors(prev => withoutKey(prev, id));
        // Mutual exclusion: editing the categorize picker clears the dismiss draft.
        setDismissDrafts(prev => withoutKey(prev, id));
        setUserOverrides(prev => {
            const next = new Map(prev);
            const existing = next.get(id) ?? {};
            next.set(id, { ...existing, ...patch });
            return next;
        });
    }

    function resetRow(id: BankTransactionId) {
        setUserOverrides(prev => withoutKey(prev, id));
        setDismissDrafts(prev => withoutKey(prev, id));
        setRowErrors(prev => withoutKey(prev, id));
    }

    // ── Selection ────────────────────────────────────────────────────────────
    // RAC's GridList owns the selection mechanics (toggle, shift/keyboard range,
    // select-all); this state just mirrors the selected keys so the bulk bar and
    // Save-all can read them (ADR-0035).
    const [selection, setSelection] = useState<Set<BankTransactionId>>(new Set());
    // Bulk picker values live here so they survive Apply, Clear, and pagination
    // — re-applying the same CP+Account across page after page of similar rows
    // is the common categorization flow.
    const [bulkCounterparty, setBulkCounterparty] = useState<BulkApplyCounterparty | null>(null);
    const [bulkAccountId, setBulkAccountId] = useState<AccountId | null>(null);

    const visibleIds = useMemo(() => visibleBts.map(b => b.id), [visibleBts]);

    function discardAll() {
        setUserOverrides(new Map());
        setDismissDrafts(new Map());
        setRowErrors(new Map());
        setSelection(new Set());
    }

    function onSelectionChange(keys: Selection) {
        // "all" maps to every visible row on this page, preserving the
        // page-bound, self-pruning behavior of the old select-all sentinel.
        setSelection(
            keys === 'all' ? new Set(visibleIds) : new Set(keys as Set<BankTransactionId>),
        );
    }

    function onToggleSelectAll() {
        const allSelected = visibleIds.length > 0 && visibleIds.every(id => selection.has(id));
        setSelection(allSelected ? new Set() : new Set(visibleIds));
    }

    function visibleSelection(): BankTransactionId[] {
        const out: BankTransactionId[] = [];
        for (const id of selection) {
            if (visibleIds.includes(id)) out.push(id);
        }
        return out;
    }

    function applyBulk(input: BulkApplyInput) {
        if (input.counterparty === null && input.accountId === null) return;
        const targets = visibleSelection();
        setUserOverrides(prev => {
            const next = new Map(prev);
            for (const id of targets) {
                next.set(id, applyBulkPatchToOverride(prev.get(id), input));
            }
            return next;
        });
        // Mutual exclusion: bulk-applying a CP / Account clears any dismiss draft
        // on those rows (issue #86).
        setDismissDrafts(prev => removeKeysFor(prev, targets));
        setRowErrors(prev => removeKeysFor(prev, targets));
    }

    function applyBulkSuggestions() {
        const targets = visibleSelection();
        if (targets.length === 0) return;

        let filled = 0;
        let cpOnly = 0;
        let pending = 0;
        let noMatch = 0;
        const patches = new Map<BankTransactionId, Partial<RowDraft>>();
        for (const id of targets) {
            const bt = visibleBts.find(b => b.id === id);
            if (!bt) continue;
            const ibanCpId = ibanResolvedCpByBt.get(id) ?? null;
            if (ibanCpId !== null && suggestionPendingByCpId.has(ibanCpId)) {
                pending += 1;
                continue;
            }
            const ownBankSide = bankAccountsById.get(bt.bankAccountId)?.accountId ?? null;
            const patch = buildSuggestionOverride(
                bt,
                bankAccounts,
                suggestionsByCpId,
                accountsById,
                ownBankSide,
            );
            if (patch === null) {
                noMatch += 1;
                continue;
            }
            if (patch.accountId !== undefined) filled += 1;
            else cpOnly += 1;
            patches.set(id, patch);
        }

        if (patches.size > 0) {
            const touched = [...patches.keys()];
            setUserOverrides(prev => {
                const next = new Map(prev);
                for (const [id, patch] of patches) {
                    next.set(id, { ...(prev.get(id) ?? {}), ...patch });
                }
                return next;
            });
            setDismissDrafts(prev => removeKeysFor(prev, touched));
            setRowErrors(prev => removeKeysFor(prev, touched));
        }

        const parts: string[] = [];
        if (filled > 0) parts.push(t`${filled.toString()} filled`);
        if (cpOnly > 0) parts.push(t`${cpOnly.toString()} got counterparty only`);
        if (pending > 0) parts.push(t`${pending.toString()} still loading`);
        if (noMatch > 0) parts.push(t`${noMatch.toString()} had no suggestion`);
        const msg = parts.length > 0 ? parts.join(', ') : t`No suggestions to apply.`;
        if (filled === 0 && cpOnly === 0) toast.error(msg);
        else toast.success(msg);
    }

    function applyBulkDismiss(reason: string) {
        const trimmed = reason.trim();
        if (trimmed.length === 0) return;
        const targets = visibleSelection();
        setDismissDrafts(prev => setBulkDismissDrafts(prev, targets, trimmed));
        // Mutual exclusion: setting a dismiss draft clears any in-progress
        // categorize draft for that row.
        setUserOverrides(prev => removeKeysFor(prev, targets));
        setRowErrors(prev => removeKeysFor(prev, targets));
    }

    // Filtered to visible rows: an entry can linger in the selection set after
    // a row leaves `visibleBts` (e.g. saved optimistically), and the footer
    // count + visibility need to match what the user can still act on.
    const selectedBts = useMemo(
        () => visibleBts.filter(b => selection.has(b.id)),
        [visibleBts, selection],
    );
    const selectedCurrencies = useMemo(() => distinctRowCurrencies(selectedBts), [selectedBts]);
    const ownBankSideAccountIdsInSelection = useMemo(() => {
        const s = new Set<AccountId>();
        for (const bt of selectedBts) {
            const baAccount = bankAccountsById.get(bt.bankAccountId)?.accountId;
            if (baAccount) s.add(baAccount);
        }
        return s;
    }, [selectedBts, bankAccountsById]);
    const selectionCount = selectedBts.length;

    const readyIds = useMemo(
        () =>
            visibleBts
                .map(bt => bt.id)
                .filter(id => dismissDrafts.has(id) || rowStatus(draftFor(id)) === 'ready'),
        // draftFor depends on userOverrides + prefillByBt
        // eslint-disable-next-line react-hooks/exhaustive-deps
        [visibleBts, userOverrides, dismissDrafts, prefillByBt],
    );

    const dirtyCount = useMemo(() => {
        let n = 0;
        for (const id of userOverrides.keys()) {
            // A row with a dismiss-draft has its userOverride cleared, so it
            // won't be in this iteration. Dismiss-drafts add to dirty below.
            if (!isRowPristine(id)) n += 1;
        }
        n += dismissDrafts.size;
        return n;
        // isRowPristine depends on userOverrides + prefillByBt
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [userOverrides, dismissDrafts, prefillByBt]);

    const [saving, setSaving] = useState(false);
    const [progress, setProgress] = useState<{ done: number; total: number } | null>(null);
    const [discardOpen, setDiscardOpen] = useState(false);
    const [bulkDismissOpen, setBulkDismissOpen] = useState(false);

    async function saveAll() {
        if (readyIds.length === 0) return;
        setSaving(true);
        setProgress({ done: 0, total: readyIds.length });
        const readyRows = readyIds
            .map(id => {
                const bt = visibleBts.find(b => b.id === id);
                if (!bt) return null;
                const dismissReason = dismissDrafts.get(id);
                if (dismissReason !== undefined) {
                    return {
                        id,
                        bt,
                        action: { kind: 'dismiss' as const, reason: dismissReason },
                    };
                }
                return {
                    id,
                    bt,
                    action: { kind: 'categorize' as const, draft: draftFor(id) },
                };
            })
            .filter((r): r is NonNullable<typeof r> => r !== null);

        const summary = await runSaveAll(readyRows, {
            createCounterparty: async name => {
                const wire = await postJson<WireCounterparty>(
                    '/api/counterparties',
                    { name },
                    'create counterparty',
                );
                return asCounterpartyId(wire.id);
            },
            categorize: async (id, request: WireCategorizeRequest) => {
                await postJson<WireJournalEntry>(
                    `/api/bank-transactions/${id}/categorize`,
                    request,
                    'categorize bank transaction',
                );
            },
            dismiss: async (id, reason) => {
                await postJson<components['schemas']['BankTransactionOutput']>(
                    `/api/bank-transactions/${id}/dismiss`,
                    { reason },
                    'dismiss bank transaction',
                );
            },
            onProgress: (done, total) => {
                setProgress({ done, total });
            },
            onRowResult: (id, outcome: SaveAllOutcome) => {
                if (outcome.ok) {
                    setSavedIds(prev => new Set(prev).add(id));
                    setUserOverrides(prev => withoutKey(prev, id));
                    setDismissDrafts(prev => withoutKey(prev, id));
                } else {
                    setRowErrors(prev => new Map(prev).set(id, outcome.error));
                }
            },
        });

        await queryClient.invalidateQueries({ queryKey: bankTransactionsKeys.all });
        await queryClient.invalidateQueries({ queryKey: journalEntriesKeys.all });
        await queryClient.invalidateQueries({ queryKey: counterpartiesKeys.all });
        await queryClient.invalidateQueries({ queryKey: bankAccountsKeys.all });
        await queryClient.invalidateQueries({ queryKey: accountsKeys.all });

        // Refetch settled — saved rows have left the inbox list, so drop the
        // optimistic-hidden shadow.
        setSavedIds(new Set());
        setSaving(false);
        setProgress(null);
        toast.push(formatSaveAllToast(summary), summary.failed === 0 ? 'success' : 'error');
    }

    const blocker = useBlocker({
        shouldBlockFn: () => dirtyCount > 0 && !saving,
        enableBeforeUnload: () => dirtyCount > 0,
        withResolver: true,
    });

    const counterpartyItems = useMemo(
        () => buildCounterpartyItems(counterparties),
        [counterparties],
    );

    const actionBarProps: ActionBarProps = {
        selectionCount,
        selectedCurrencies,
        bulkCounterparty,
        bulkAccountId,
        counterpartyItems,
        bulkCurrency: selectedCurrencies.length === 1 ? (selectedCurrencies[0] ?? null) : null,
        excludeAccountIds: ownBankSideAccountIdsInSelection,
        saving,
        progress,
        dirtyCount,
        readyCount: readyIds.length,
        onBulkCounterpartyChange: setBulkCounterparty,
        onBulkAccountIdChange: setBulkAccountId,
        onApply: () => {
            applyBulk({ counterparty: bulkCounterparty, accountId: bulkAccountId });
        },
        onApplySuggestions: applyBulkSuggestions,
        onBulkDismiss: () => {
            setBulkDismissOpen(true);
        },
        onClearSelection: () => {
            setSelection(new Set());
        },
        onSave: () => void saveAll(),
        onDiscard: () => {
            setDiscardOpen(true);
        },
    };

    return {
        actionBarProps,
        visibleBts,
        visibleIds,
        selection,
        saving,
        prefillByBt,
        dismissDrafts,
        rowErrors,
        counterpartyItems,
        bankAccountsById,
        dirtyCount,
        selectionCount,
        bulkDismissOpen,
        setBulkDismissOpen,
        discardOpen,
        setDiscardOpen,
        blocker,
        draftFor,
        isRowPristine,
        patchDraft,
        resetRow,
        onSelectionChange,
        onToggleSelectAll,
        applyBulkDismiss,
        discardAll,
    };
}

function withoutKey<K, V>(map: Map<K, V>, key: K): Map<K, V> {
    if (!map.has(key)) return map;
    const next = new Map(map);
    next.delete(key);
    return next;
}

function buildCounterpartyItems(
    counterparties: Counterparty[],
): ComboBoxItem<CounterpartyId | null>[] {
    return [...counterparties]
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(c => ({ key: c.id, label: c.name, value: c.id }));
}

function formatSaveAllToast(summary: SaveAllSummary): string {
    return t`${summary.categorized.toString()} categorized, ${summary.dismissed.toString()} dismissed, ${summary.failed.toString()} failed.`;
}
