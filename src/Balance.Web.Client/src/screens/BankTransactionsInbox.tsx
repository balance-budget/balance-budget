import { useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link, useBlocker } from '@tanstack/react-router';
import { useQueries, useQueryClient } from '@tanstack/react-query';
import { useAccounts, type Account } from '../api/accounts';
import { useBankAccounts, type BankAccount } from '../api/bankAccounts';
import {
    BANK_TRANSACTION_FILTERS,
    bankTransactionsKeys,
    useAttachBankTransaction,
    useBankTransactions,
    useDismissBankTransaction,
    useUndismissBankTransaction,
    type BankTransaction,
    type BankTransactionFilter,
} from '../api/bankTransactions';
import {
    counterpartiesKeys,
    useCounterparties,
    type Counterparty,
    type SuggestedCounterAccount,
} from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { Combobox } from '../components/Combobox';
import { type ComboboxItem } from '../components/combobox.state';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Modal, ModalFooter } from '../components/Modal';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { SearchInput } from '../components/SearchInput';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import {
    asAccountId,
    asCounterpartyId,
    type AccountId,
    type AccountType,
    type BankTransactionId,
    type CounterpartyId,
} from '../lib/domain';
import { ApiError, getJson, postJson } from '../lib/http';
import { formatMoney } from '../lib/money';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import {
    allVisibleSelectionState,
    applyBulkPatchToOverride,
    buildSuggestionOverride,
    clearVisibleSelection,
    computeRangeSelection,
    distinctRowCurrencies,
    emptyDraft,
    isPristine,
    removeKeysFor,
    resolveCounterpartyByIban,
    rowStatus,
    runSaveAll,
    selectAllVisible,
    setBulkDismissDrafts,
    toggleSelection,
    type AllVisibleSelectionState,
    type BulkApplyCounterparty,
    type BulkApplyInput,
    type RowDraft,
    type RowStatus,
    type SaveAllOutcome,
    type SaveAllSummary,
} from './bankTransactionsInbox.state';
import type { components } from '../lib/api-types';

type WireCounterparty = components['schemas']['CounterpartyOutput'];
type WireJournalEntry = components['schemas']['JournalEntryOutput'];
type WireCategorizeRequest = components['schemas']['CategorizeBankTransactionRequest'];
type WireSuggested = components['schemas']['SuggestedCounterAccountOutput'];

const PAGE_SIZE = 50;

const FILTER_LABEL: Record<BankTransactionFilter, string> = {
    Inbox: 'Inbox',
    Matched: 'Matched',
    Dismissed: 'Dismissed',
    All: 'All',
};

const SUBTITLE: Record<BankTransactionFilter, string> = {
    Inbox: 'Bank rows waiting for a journal entry. Pick Counterparty and Account inline; Save all when you’re happy.',
    Matched: 'Bank rows that have been categorised into a journal entry.',
    Dismissed: 'Bank rows you marked as not needing a journal entry.',
    All: 'Every imported bank row, regardless of state.',
};

const EMPTY_TITLE: Record<BankTransactionFilter, string> = {
    Inbox: "You're caught up.",
    Matched: 'Nothing categorised yet.',
    Dismissed: 'Nothing dismissed.',
    All: 'No bank transactions yet.',
};

const EMPTY_HINT: Record<BankTransactionFilter, string> = {
    Inbox: 'Imported rows that need categorising will appear here.',
    Matched: 'Categorise an inbox row to see it here.',
    Dismissed: 'Dismissed rows live here for audit.',
    All: 'Import a bank statement from Bank imports to get started.',
};

const ACCOUNT_TYPE_ORDER: AccountType[] = ['Asset', 'Liability', 'Income', 'Expense', 'Equity'];

const ACCOUNT_TYPE_LABEL: Record<AccountType, string> = {
    Asset: 'Assets',
    Liability: 'Liabilities',
    Income: 'Income',
    Expense: 'Expenses',
    Equity: 'Equity',
};

type Props = {
    page: number;
    filter: BankTransactionFilter;
    q: string;
    onPageChange: (page: number) => void;
    onFilterChange: (filter: BankTransactionFilter) => void;
    onSearchChange: (q: string) => void;
};

export function BankTransactionsInbox({
    page,
    filter,
    q,
    onPageChange,
    onFilterChange,
    onSearchChange,
}: Props) {
    const skip = (page - 1) * PAGE_SIZE;
    const debouncedQ = useDebouncedValue(q, 200);
    const query = useBankTransactions(skip, PAGE_SIZE, filter, debouncedQ);
    const catalog = useCurrencyCatalog();
    const [dismissing, setDismissing] = useState<BankTransaction | null>(null);

    return (
        <>
            <Panel>
                <SectionHead title="Bank transactions" subtitle={SUBTITLE[filter]} />
                <div className="flex flex-col gap-3 mb-4">
                    <FilterChips value={filter} onChange={onFilterChange} />
                    <SearchInput
                        value={q}
                        onChange={onSearchChange}
                        placeholder="Search description or counterparty…"
                    />
                </div>
                <Body
                    query={query}
                    catalog={catalog}
                    filter={filter}
                    page={page}
                    onPageChange={onPageChange}
                    onDismiss={setDismissing}
                />
            </Panel>

            {dismissing && (
                <DismissDialog
                    bankTransaction={dismissing}
                    onClose={() => {
                        setDismissing(null);
                    }}
                />
            )}
        </>
    );
}

function FilterChips({
    value,
    onChange,
}: {
    value: BankTransactionFilter;
    onChange: (filter: BankTransactionFilter) => void;
}) {
    return (
        <div className="flex items-center gap-2" role="tablist" aria-label="Filter">
            {BANK_TRANSACTION_FILTERS.map(filter => {
                const active = filter === value;
                return (
                    <button
                        key={filter}
                        type="button"
                        role="tab"
                        aria-selected={active}
                        onClick={() => {
                            onChange(filter);
                        }}
                        className={cx(
                            'px-3 py-1 rounded-sm text-[12px] font-medium select-none transition-colors',
                            active
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        )}
                    >
                        {FILTER_LABEL[filter]}
                    </button>
                );
            })}
        </div>
    );
}

function Body({
    query,
    catalog,
    filter,
    page,
    onPageChange,
    onDismiss,
}: {
    query: ReturnType<typeof useBankTransactions>;
    catalog: CurrencyCatalog;
    filter: BankTransactionFilter;
    page: number;
    onPageChange: (page: number) => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    if (query.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
            </div>
        );
    }

    if (query.isError) {
        return (
            <ErrorState
                message="Couldn't load bank transactions."
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.items.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">{EMPTY_TITLE[filter]}</span>
                <span className="text-[12px] text-fg-3">{EMPTY_HINT[filter]}</span>
            </div>
        );
    }

    if (filter === 'Inbox') {
        return (
            <InboxEditor
                bankTransactions={query.data.items}
                totalCount={query.data.totalCount}
                catalog={catalog}
                page={page}
                onPageChange={onPageChange}
                onDismiss={onDismiss}
            />
        );
    }

    return (
        <ReadOnlyList
            bankTransactions={query.data.items}
            totalCount={query.data.totalCount}
            catalog={catalog}
            page={page}
            onPageChange={onPageChange}
            onDismiss={onDismiss}
        />
    );
}

function ReadOnlyList({
    bankTransactions,
    totalCount,
    catalog,
    page,
    onPageChange,
    onDismiss,
}: {
    bankTransactions: BankTransaction[];
    totalCount: number;
    catalog: CurrencyCatalog;
    page: number;
    onPageChange: (page: number) => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    return (
        <div className="flex flex-col">
            <div className="hidden lg:grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px_minmax(180px,200px)] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span>Description</span>
                <span>Counterparty</span>
                <span className="text-right">Amount</span>
                <span className="text-right">Actions</span>
            </div>
            {bankTransactions.map(bt => (
                <ReadOnlyRow
                    key={bt.id}
                    bankTransaction={bt}
                    catalog={catalog}
                    onDismiss={onDismiss}
                />
            ))}
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={totalCount}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function ReadOnlyRow({
    bankTransaction,
    catalog,
    onDismiss,
}: {
    bankTransaction: BankTransaction;
    catalog: CurrencyCatalog;
    onDismiss: (bt: BankTransaction) => void;
}) {
    return (
        <div className="border-b border-border-soft last:border-b-0">
            <div className="hidden lg:grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px_minmax(180px,200px)] gap-3 items-center px-2 py-2">
                <span className="text-[12px] text-fg-3 tabular">{bankTransaction.bookingDate}</span>
                <div className="min-w-0 flex flex-col leading-tight">
                    <span className="text-[13px] text-fg-1 truncate">
                        {bankTransaction.description}
                    </span>
                    <StateChip bankTransaction={bankTransaction} />
                </div>
                <CounterpartyCell bankTransaction={bankTransaction} />
                <AmountCell bankTransaction={bankTransaction} catalog={catalog} />
                <ReadOnlyActions bankTransaction={bankTransaction} onDismiss={onDismiss} />
            </div>
            <div className="lg:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-center justify-between gap-3">
                    <span className="text-[12px] text-fg-3 tabular">
                        {bankTransaction.bookingDate}
                    </span>
                    <AmountCell bankTransaction={bankTransaction} catalog={catalog} />
                </div>
                <span className="text-[13px] text-fg-1 truncate">
                    {bankTransaction.description}
                </span>
                <CounterpartyCell bankTransaction={bankTransaction} />
                <StateChip bankTransaction={bankTransaction} />
                <div className="pt-1">
                    <ReadOnlyActions bankTransaction={bankTransaction} onDismiss={onDismiss} />
                </div>
            </div>
        </div>
    );
}

function StateChip({ bankTransaction }: { bankTransaction: BankTransaction }) {
    if (bankTransaction.journalEntryId) {
        return <span className="text-[11px] text-success tabular">Categorised</span>;
    }
    if (bankTransaction.dismissedAt) {
        const reason = bankTransaction.dismissedReason ?? '';
        return (
            <span className="text-[11px] text-fg-3 tabular truncate">
                Dismissed{reason ? ` · ${reason}` : ''}
            </span>
        );
    }
    return null;
}

function CounterpartyCell({ bankTransaction }: { bankTransaction: BankTransaction }) {
    const name = bankTransaction.counterpartyName;
    const iban = bankTransaction.counterpartyAccountNumber;
    if (!name && !iban) {
        return <span className="text-[12px] text-fg-3">—</span>;
    }
    return (
        <div className="min-w-0 flex flex-col leading-tight">
            <span className="text-[12px] text-fg-2 truncate">{name ?? '—'}</span>
            {iban && <span className="text-[11px] text-fg-3 truncate tabular">{iban}</span>}
        </div>
    );
}

function AmountCell({
    bankTransaction,
    catalog,
}: {
    bankTransaction: BankTransaction;
    catalog: CurrencyCatalog;
}) {
    const money = bankTransaction.money;
    const colour = money.amount < 0 ? 'text-danger' : 'text-success';
    return (
        <span className={cx('font-mono text-[13px] tabular text-right', colour)}>
            {formatMoney(money.amount, money.currencyCode, catalog, { sign: true })}
        </span>
    );
}

function ReadOnlyActions({
    bankTransaction,
    onDismiss,
}: {
    bankTransaction: BankTransaction;
    onDismiss: (bt: BankTransaction) => void;
}) {
    if (bankTransaction.journalEntryId) {
        return <div />;
    }
    if (bankTransaction.dismissedAt) {
        return <UndismissButton bankTransaction={bankTransaction} />;
    }
    return (
        <div className="flex items-center justify-end gap-1">
            <Link
                to="/bank-transactions/$id/categorize"
                params={{ id: bankTransaction.id }}
                aria-label="Categorise"
                className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-brand-primary hover:bg-brand-primary-soft"
            >
                <Icon name="check-circle" size={14} strokeWidth={2} />
                Categorise
            </Link>
            <button
                type="button"
                onClick={() => {
                    onDismiss(bankTransaction);
                }}
                aria-label="Dismiss"
                className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-fg-2 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="x" size={14} strokeWidth={2} />
                Dismiss
            </button>
        </div>
    );
}

// ─────────────────────────────────────────────────────────────────────────────
// Inbox editor: per-row draft buffer, top Save-all bar, navigation guard.
// ─────────────────────────────────────────────────────────────────────────────

function InboxEditor({
    bankTransactions,
    totalCount,
    catalog,
    page,
    onPageChange,
    onDismiss,
}: {
    bankTransactions: BankTransaction[];
    totalCount: number;
    catalog: CurrencyCatalog;
    page: number;
    onPageChange: (page: number) => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    const accounts = useAccounts();
    const counterparties = useCounterparties();
    const bankAccounts = useBankAccounts();

    if (accounts.isPending || counterparties.isPending || bankAccounts.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
            </div>
        );
    }
    if (accounts.isError) {
        return (
            <ErrorState message="Couldn't load accounts." onRetry={() => void accounts.refetch()} />
        );
    }
    if (counterparties.isError) {
        return (
            <ErrorState
                message="Couldn't load counterparties."
                onRetry={() => void counterparties.refetch()}
            />
        );
    }
    if (bankAccounts.isError) {
        return (
            <ErrorState
                message="Couldn't load bank accounts."
                onRetry={() => void bankAccounts.refetch()}
            />
        );
    }

    return (
        <InboxEditorReady
            bankTransactions={bankTransactions}
            totalCount={totalCount}
            accounts={accounts.data}
            counterparties={counterparties.data}
            bankAccounts={bankAccounts.data}
            catalog={catalog}
            page={page}
            onPageChange={onPageChange}
            onDismiss={onDismiss}
        />
    );
}

function InboxEditorReady({
    bankTransactions,
    totalCount,
    accounts,
    counterparties,
    bankAccounts,
    catalog,
    page,
    onPageChange,
    onDismiss,
}: {
    bankTransactions: BankTransaction[];
    totalCount: number;
    accounts: Account[];
    counterparties: Counterparty[];
    bankAccounts: BankAccount[];
    catalog: CurrencyCatalog;
    page: number;
    onPageChange: (page: number) => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
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

    // Inbox-suggestion-gating amendment to ADR 0014: rows render pristine —
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
        // Mutual exclusion: editing the categorise picker clears the dismiss draft.
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
    const [selection, setSelection] = useState<Set<BankTransactionId>>(new Set());
    const selectionAnchorRef = useRef<BankTransactionId | null>(null);
    function setSelectionAnchor(id: BankTransactionId | null) {
        selectionAnchorRef.current = id;
    }

    const visibleIds = useMemo(() => visibleBts.map(b => b.id), [visibleBts]);

    function discardAll() {
        setUserOverrides(new Map());
        setDismissDrafts(new Map());
        setRowErrors(new Map());
        setSelection(new Set());
        setSelectionAnchor(null);
    }

    function onRowCheckboxClick(id: BankTransactionId, shiftKey: boolean) {
        const anchor = selectionAnchorRef.current;
        if (shiftKey && anchor !== null) {
            setSelection(prev => computeRangeSelection(visibleIds, prev, anchor, id));
        } else {
            setSelection(prev => toggleSelection(prev, id));
        }
        setSelectionAnchor(id);
    }

    function onHeaderCheckboxClick() {
        const state = allVisibleSelectionState(selection, visibleIds);
        if (state === 'all') {
            setSelection(prev => clearVisibleSelection(prev, visibleIds));
        } else {
            setSelection(prev => selectAllVisible(prev, visibleIds));
        }
        setSelectionAnchor(null);
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
        const touched: BankTransactionId[] = [];
        setUserOverrides(prev => {
            const next = new Map(prev);
            for (const id of targets) {
                const bt = visibleBts.find(b => b.id === id);
                if (!bt) continue;
                const ownBankSide = bankAccountsById.get(bt.bankAccountId)?.accountId ?? null;
                const patch = buildSuggestionOverride(
                    bt,
                    bankAccounts,
                    suggestionsByCpId,
                    accountsById,
                    ownBankSide,
                );
                if (patch === null) continue;
                touched.push(id);
                next.set(id, { ...(prev.get(id) ?? {}), ...patch });
            }
            return next;
        });
        setDismissDrafts(prev => removeKeysFor(prev, touched));
        setRowErrors(prev => removeKeysFor(prev, touched));
    }

    function applyBulkDismiss(reason: string) {
        const trimmed = reason.trim();
        if (trimmed.length === 0) return;
        const targets = visibleSelection();
        setDismissDrafts(prev => setBulkDismissDrafts(prev, targets, trimmed));
        // Mutual exclusion: setting a dismiss draft clears any in-progress
        // categorise draft for that row.
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
                    action: { kind: 'categorise' as const, draft: draftFor(id) },
                };
            })
            .filter((r): r is NonNullable<typeof r> => r !== null);

        const summary = await runSaveAll(readyRows, {
            createCounterparty: async name => {
                const wire = await postJson<WireCounterparty>(
                    '/api/counterparties',
                    { name },
                    new AbortController().signal,
                    'create counterparty',
                );
                return asCounterpartyId(wire.id);
            },
            categorize: async (id, request: WireCategorizeRequest) => {
                await postJson<WireJournalEntry>(
                    `/api/bank-transactions/${id}/categorize`,
                    request,
                    new AbortController().signal,
                    'categorise bank transaction',
                );
            },
            dismiss: async (id, reason) => {
                await postJson<components['schemas']['BankTransactionOutput']>(
                    `/api/bank-transactions/${id}/dismiss`,
                    { reason },
                    new AbortController().signal,
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
        await queryClient.invalidateQueries({ queryKey: ['journalEntries'] });
        await queryClient.invalidateQueries({ queryKey: counterpartiesKeys.all });
        await queryClient.invalidateQueries({ queryKey: ['bank-accounts'] });
        await queryClient.invalidateQueries({ queryKey: ['accounts'] });

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

    return (
        <div
            className={cx(
                'flex flex-col',
                // Reserve clearance under the fixed BulkApplyFooter so it doesn't
                // overlap Pagination or the last row when scrolled to the bottom.
                selectionCount > 0 && 'lg:pb-24',
            )}
        >
            <div className="hidden lg:flex flex-col">
                <SaveBar
                    dirtyCount={dirtyCount}
                    readyCount={readyIds.length}
                    saving={saving}
                    progress={progress}
                    onSave={() => void saveAll()}
                    onDiscard={() => {
                        setDiscardOpen(true);
                    }}
                />
                <div className="grid grid-cols-[28px_88px_1fr_minmax(180px,1.4fr)_minmax(180px,1.4fr)_120px_120px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                    <HeaderSelectAllCheckbox
                        state={allVisibleSelectionState(selection, visibleIds)}
                        onClick={onHeaderCheckboxClick}
                        disabled={saving || visibleIds.length === 0}
                    />
                    <span>Date</span>
                    <span>Description</span>
                    <span>Counterparty</span>
                    <span>Account</span>
                    <span className="text-right">Amount</span>
                    <span className="text-right">Actions</span>
                </div>
                {visibleBts.map(bt => {
                    const prefill = prefillByBt.get(bt.id);
                    if (!prefill) return null;
                    const draft = draftFor(bt.id);
                    const pristine = isRowPristine(bt.id) && !dismissDrafts.has(bt.id);
                    return (
                        <InboxRow
                            key={bt.id}
                            bankTransaction={bt}
                            draft={draft}
                            pristine={pristine}
                            dismissDraft={dismissDrafts.get(bt.id) ?? null}
                            error={rowErrors.get(bt.id) ?? null}
                            counterpartyItems={counterpartyItems}
                            accountItems={buildAccountItems(
                                accounts,
                                bt.money.currencyCode,
                                bankAccountsById.get(bt.bankAccountId)?.accountId ?? null,
                            )}
                            catalog={catalog}
                            saving={saving}
                            selected={selection.has(bt.id)}
                            onCheckboxClick={shiftKey => {
                                onRowCheckboxClick(bt.id, shiftKey);
                            }}
                            onPatch={patch => {
                                patchDraft(bt.id, patch);
                            }}
                            onReset={() => {
                                resetRow(bt.id);
                            }}
                            onDismiss={onDismiss}
                        />
                    );
                })}
                {selectionCount > 0 && (
                    <BulkApplyFooter
                        selectionCount={selectionCount}
                        selectedCurrencies={selectedCurrencies}
                        counterpartyItems={counterpartyItems}
                        accountItems={buildBulkAccountItems(
                            accounts,
                            selectedCurrencies,
                            ownBankSideAccountIdsInSelection,
                        )}
                        saving={saving}
                        onApply={applyBulk}
                        onApplySuggestions={applyBulkSuggestions}
                        onDismiss={() => {
                            setBulkDismissOpen(true);
                        }}
                        onClear={() => {
                            setSelection(new Set());
                            setSelectionAnchor(null);
                        }}
                    />
                )}
            </div>
            <div className="lg:hidden flex flex-col">
                {visibleBts.map(bt => (
                    <ReadOnlyRow
                        key={bt.id}
                        bankTransaction={bt}
                        catalog={catalog}
                        onDismiss={onDismiss}
                    />
                ))}
            </div>
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={totalCount}
                onPageChange={onPageChange}
            />

            {bulkDismissOpen && (
                <BulkDismissDialog
                    selectionCount={selectionCount}
                    onClose={() => {
                        setBulkDismissOpen(false);
                    }}
                    onConfirm={reason => {
                        applyBulkDismiss(reason);
                        setBulkDismissOpen(false);
                    }}
                />
            )}

            <ConfirmDialog
                open={discardOpen}
                onClose={() => {
                    setDiscardOpen(false);
                }}
                onConfirm={() => {
                    discardAll();
                    setDiscardOpen(false);
                }}
                title="Discard unsaved drafts?"
                message={
                    dirtyCount > 0
                        ? `Reset all ${dirtyCount.toString()} unsaved drafts back to the server-suggested values.`
                        : undefined
                }
                confirmLabel="Discard"
                variant="destructive"
            />

            {blocker.status === 'blocked' && (
                <ConfirmDialog
                    open
                    onClose={() => {
                        blocker.reset();
                    }}
                    onConfirm={() => {
                        blocker.proceed();
                    }}
                    title="Leave with unsaved drafts?"
                    message={`You have ${dirtyCount.toString()} unsaved draft${
                        dirtyCount === 1 ? '' : 's'
                    }. Leaving will discard them.`}
                    confirmLabel="Leave"
                    variant="destructive"
                />
            )}
        </div>
    );
}

function withoutKey<K, V>(map: Map<K, V>, key: K): Map<K, V> {
    if (!map.has(key)) return map;
    const next = new Map(map);
    next.delete(key);
    return next;
}

function buildCounterpartyItems(
    counterparties: Counterparty[],
): ComboboxItem<CounterpartyId | null>[] {
    return [...counterparties]
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(c => ({ key: c.id, label: c.name, value: c.id }));
}

function buildAccountItems(
    accounts: Account[],
    currencyCode: string,
    ownBankSideAccountId: AccountId | null,
): ComboboxItem<AccountId>[] {
    return accounts
        .filter(a => a.currencyCode === currencyCode && a.id !== ownBankSideAccountId)
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(a => ({ key: a.id, label: a.name, group: a.type, value: a.id }));
}

/** Bulk-apply Account picker items. Empty when the selection spans more than
 *  one currency (the picker is also disabled in that case). Excludes any
 *  bank-side account that maps to a selected row's bank, so the user can't
 *  bulk-pick the very account on the BT side of those rows. */
function buildBulkAccountItems(
    accounts: Account[],
    selectedCurrencies: readonly string[],
    excludedAccountIds: ReadonlySet<AccountId>,
): ComboboxItem<AccountId>[] {
    if (selectedCurrencies.length !== 1) return [];
    const currency = selectedCurrencies[0];
    return accounts
        .filter(a => a.currencyCode === currency && !excludedAccountIds.has(a.id))
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(a => ({ key: a.id, label: a.name, group: a.type, value: a.id }));
}

function RowSelectCheckbox({
    selected,
    disabled,
    onClick,
    ariaLabel,
}: {
    selected: boolean;
    disabled: boolean;
    onClick: (shiftKey: boolean) => void;
    ariaLabel: string;
}) {
    // Capture shift via mousedown/keydown — onChange doesn't carry modifier
    // flags. We avoid the onClick + preventDefault pattern because it leaves
    // the controlled checkbox's internal value tracker out of sync with React
    // state in some browsers, which surfaced as the wrong row being toggled.
    const shiftRef = useRef(false);
    return (
        <input
            type="checkbox"
            aria-label={ariaLabel}
            checked={selected}
            disabled={disabled}
            onMouseDown={e => {
                shiftRef.current = e.shiftKey;
            }}
            onKeyDown={e => {
                shiftRef.current = e.shiftKey;
            }}
            onChange={() => {
                onClick(shiftRef.current);
                shiftRef.current = false;
            }}
            className="h-4 w-4 cursor-pointer accent-brand-primary disabled:opacity-40 disabled:cursor-not-allowed"
        />
    );
}

function HeaderSelectAllCheckbox({
    state,
    onClick,
    disabled,
}: {
    state: AllVisibleSelectionState;
    onClick: () => void;
    disabled: boolean;
}) {
    function setRef(el: HTMLInputElement | null) {
        if (el) el.indeterminate = state === 'some';
    }
    return (
        <input
            ref={setRef}
            type="checkbox"
            aria-label="Select all visible rows"
            checked={state === 'all'}
            disabled={disabled}
            onChange={onClick}
            className="h-4 w-4 cursor-pointer accent-brand-primary disabled:opacity-40 disabled:cursor-not-allowed"
        />
    );
}

function BulkApplyFooter({
    selectionCount,
    selectedCurrencies,
    counterpartyItems,
    accountItems,
    saving,
    onApply,
    onApplySuggestions,
    onDismiss,
    onClear,
}: {
    selectionCount: number;
    selectedCurrencies: readonly string[];
    counterpartyItems: ComboboxItem<CounterpartyId | null>[];
    accountItems: ComboboxItem<AccountId>[];
    saving: boolean;
    onApply: (input: BulkApplyInput) => void;
    onApplySuggestions: () => void;
    onDismiss: () => void;
    onClear: () => void;
}) {
    const [counterparty, setCounterparty] = useState<BulkApplyCounterparty | null>(null);
    const [accountId, setAccountId] = useState<AccountId | null>(null);

    const mixedCurrency = selectedCurrencies.length > 1;
    const accountDisabled = saving || mixedCurrency;
    const canApply = !saving && (counterparty !== null || accountId !== null);

    const cpValue: CounterpartyId | null =
        counterparty?.kind === 'existing' ? counterparty.counterpartyId : null;
    const cpItemsWithPending = useMemo(() => {
        if (counterparty?.kind !== 'new' || counterparty.name.trim().length === 0) {
            return counterpartyItems;
        }
        const pending: ComboboxItem<CounterpartyId | null> = {
            key: '__pending_bulk__',
            label: `${counterparty.name.trim()} (new)`,
            value: null,
        };
        return [pending, ...counterpartyItems];
    }, [counterparty, counterpartyItems]);

    function handleApply() {
        if (!canApply) return;
        onApply({ counterparty, accountId });
        setCounterparty(null);
        setAccountId(null);
    }

    // Portal to body to escape the Panel's `backdrop-blur-card` — backdrop-filter
    // creates a containing block for fixed descendants, which would anchor the
    // bar to the Panel rather than the viewport and put it offscreen when
    // scrolled to the top of a long list.
    return createPortal(
        <div className="fixed bottom-6 left-[calc(15rem+2rem)] right-8 z-30 px-3 py-2 rounded-sm bg-bg-1 border border-brand-primary/30 shadow-overlay">
            <div className="flex flex-wrap items-center gap-3">
                <span className="text-[12px] font-medium text-fg-1">
                    {selectionCount.toString()} selected
                </span>
                <div className="min-w-[180px] flex-1 max-w-[260px]">
                    <Combobox
                        items={cpItemsWithPending}
                        value={cpValue}
                        onChange={id => {
                            setCounterparty({ kind: 'existing', counterpartyId: id });
                        }}
                        onClear={() => {
                            setCounterparty({ kind: 'existing', counterpartyId: null });
                        }}
                        onCreate={typed => {
                            setCounterparty({ kind: 'new', name: typed });
                        }}
                        noneLabel="── None (self-transfer)"
                        createLabel={typed => `+ Create '${typed}'`}
                        placeholder="Counterparty…"
                        disabled={saving}
                        ariaLabel="Bulk counterparty"
                    />
                </div>
                <div className="min-w-[180px] flex-1 max-w-[260px]">
                    <Combobox
                        items={accountItems}
                        value={accountId}
                        onChange={id => {
                            setAccountId(id);
                        }}
                        groupOrder={ACCOUNT_TYPE_ORDER}
                        groupLabels={ACCOUNT_TYPE_LABEL}
                        placeholder={mixedCurrency ? 'Account (mixed currencies)' : 'Account…'}
                        disabled={accountDisabled}
                        ariaLabel="Bulk account"
                    />
                </div>
                <button
                    type="button"
                    onClick={handleApply}
                    disabled={!canApply}
                    className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                >
                    Apply to {selectionCount.toString()} selected
                </button>
                <button
                    type="button"
                    onClick={onApplySuggestions}
                    disabled={saving}
                    title="Fill the selected rows with the IBAN-matched counterparty and the last-used account for that counterparty."
                    className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-1 border border-border-strong hover:bg-surface-2 disabled:opacity-60"
                >
                    Apply suggestions
                </button>
                <button
                    type="button"
                    onClick={onDismiss}
                    disabled={saving}
                    className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-1 border border-border-strong hover:bg-surface-2 disabled:opacity-60"
                >
                    Dismiss with reason…
                </button>
                <button
                    type="button"
                    onClick={onClear}
                    disabled={saving}
                    className="px-2 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                >
                    Clear
                </button>
            </div>
            {mixedCurrency && (
                <p className="mt-1 text-[11px] text-fg-3">
                    Selected rows span {selectedCurrencies.join(' + ')} — Account can&apos;t be
                    bulk-applied.
                </p>
            )}
        </div>,
        document.body,
    );
}

function SaveBar({
    dirtyCount,
    readyCount,
    saving,
    progress,
    onSave,
    onDiscard,
}: {
    dirtyCount: number;
    readyCount: number;
    saving: boolean;
    progress: { done: number; total: number } | null;
    onSave: () => void;
    onDiscard: () => void;
}) {
    if (dirtyCount === 0 && !saving) {
        return null;
    }
    return (
        <div className="flex items-center justify-between gap-3 mb-3 px-3 py-2 rounded-sm bg-brand-primary-soft border border-brand-primary/30">
            <div className="flex items-center gap-3 text-[12px] text-fg-2">
                {saving && progress ? (
                    <span className="tabular">
                        Saving {progress.done.toString()}/{progress.total.toString()}…
                    </span>
                ) : (
                    <span>
                        {dirtyCount.toString()} unsaved · {readyCount.toString()} ready
                    </span>
                )}
            </div>
            <div className="flex items-center gap-2">
                <button
                    type="button"
                    onClick={onDiscard}
                    disabled={saving}
                    className="px-3 py-1 rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                >
                    Discard
                </button>
                <button
                    type="button"
                    onClick={onSave}
                    disabled={saving || readyCount === 0}
                    className="px-3 py-1 rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                >
                    {saving
                        ? 'Saving…'
                        : `Save ${readyCount.toString()} row${readyCount === 1 ? '' : 's'}`}
                </button>
            </div>
        </div>
    );
}

function InboxRow({
    bankTransaction,
    draft,
    pristine,
    dismissDraft,
    error,
    counterpartyItems,
    accountItems,
    catalog,
    saving,
    selected,
    onCheckboxClick,
    onPatch,
    onReset,
    onDismiss,
}: {
    bankTransaction: BankTransaction;
    draft: RowDraft;
    pristine: boolean;
    dismissDraft: string | null;
    error: string | null;
    counterpartyItems: ComboboxItem<CounterpartyId | null>[];
    accountItems: ComboboxItem<AccountId>[];
    catalog: CurrencyCatalog;
    saving: boolean;
    selected: boolean;
    onCheckboxClick: (shiftKey: boolean) => void;
    onPatch: (patch: Partial<RowDraft>) => void;
    onReset: () => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    const status = rowStatus(draft);
    const willDismiss = dismissDraft !== null;

    return (
        <div className="grid grid-cols-[28px_88px_1fr_minmax(180px,1.4fr)_minmax(180px,1.4fr)_120px_120px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0">
            <div className="pt-2">
                <RowSelectCheckbox
                    selected={selected}
                    disabled={saving}
                    onClick={onCheckboxClick}
                    ariaLabel={`Select bank transaction ${bankTransaction.description}`}
                />
            </div>
            <div className="flex flex-col leading-tight pt-2">
                <span className="text-[12px] text-fg-3 tabular">{bankTransaction.bookingDate}</span>
                {willDismiss ? <WillDismissIndicator /> : <StatusIndicator status={status} />}
            </div>
            <div className="min-w-0 flex flex-col leading-tight pt-2">
                <span className="text-[13px] text-fg-1 truncate">
                    {bankTransaction.description}
                </span>
                {bankTransaction.counterpartyAccountNumber && (
                    <span className="text-[11px] text-fg-3 truncate tabular">
                        {bankTransaction.counterpartyAccountNumber}
                    </span>
                )}
                {bankTransaction.matchingJournalEntry && (
                    <AttachHintBadge hint={bankTransaction.matchingJournalEntry} />
                )}
                {dismissDraft !== null && (
                    <span className="text-[11px] text-warning mt-1 truncate">
                        Reason: {dismissDraft}
                    </span>
                )}
                {error && <span className="text-[11px] text-danger mt-1">{error}</span>}
            </div>
            <CounterpartyPicker
                draft={draft}
                items={counterpartyItems}
                onPatch={onPatch}
                disabled={saving || willDismiss}
            />
            <AccountPicker
                draft={draft}
                items={accountItems}
                onPatch={onPatch}
                disabled={saving || willDismiss}
            />
            <div className="pt-2">
                <AmountCell bankTransaction={bankTransaction} catalog={catalog} />
            </div>
            <InboxRowActions
                bankTransaction={bankTransaction}
                pristine={pristine}
                disabled={saving}
                onReset={onReset}
                onDismiss={onDismiss}
            />
        </div>
    );
}

function StatusIndicator({ status }: { status: RowStatus }) {
    if (status === 'ready') {
        return (
            <span className="text-[11px] text-success tabular inline-flex items-center gap-1">
                <span aria-hidden>●</span> ready
            </span>
        );
    }
    if (status === 'invalid') {
        return (
            <span className="text-[11px] text-warning tabular inline-flex items-center gap-1">
                <span aria-hidden>⚠</span> invalid
            </span>
        );
    }
    return (
        <span className="text-[11px] text-fg-3 tabular inline-flex items-center gap-1">
            <span aria-hidden>—</span>
        </span>
    );
}

function AttachHintBadge({ hint }: { hint: NonNullable<BankTransaction['matchingJournalEntry']> }) {
    return (
        <span
            className="text-[11px] text-brand-primary mt-1 truncate inline-flex items-center gap-1"
            title={`Auto-matched to JE on ${hint.date}`}
        >
            <Icon name="link" size={11} strokeWidth={2} />
            Matches JE · {hint.otherAccountName}
        </span>
    );
}

function WillDismissIndicator() {
    return (
        <span className="text-[11px] text-warning tabular inline-flex items-center gap-1">
            <span aria-hidden>●</span> will dismiss
        </span>
    );
}

function CounterpartyPicker({
    draft,
    items,
    onPatch,
    disabled,
}: {
    draft: RowDraft;
    items: ComboboxItem<CounterpartyId | null>[];
    onPatch: (patch: Partial<RowDraft>) => void;
    disabled: boolean;
}) {
    // Render the in-progress "new" name as a synthetic item, so the user sees
    // what they typed across renders.
    const effectiveItems = useMemo(() => {
        if (draft.counterpartyMode !== 'new' || draft.newCounterpartyName.trim().length === 0) {
            return items;
        }
        const pending: ComboboxItem<CounterpartyId | null> = {
            key: '__pending__',
            label: `${draft.newCounterpartyName.trim()} (new)`,
            value: null,
        };
        return [pending, ...items];
    }, [draft.counterpartyMode, draft.newCounterpartyName, items]);

    const value: CounterpartyId | null =
        draft.counterpartyMode === 'existing' ? draft.counterpartyId : null;

    return (
        <Combobox
            items={effectiveItems}
            value={value}
            onChange={id => {
                onPatch({
                    counterpartyMode: 'existing',
                    counterpartyId: id,
                    newCounterpartyName: '',
                });
            }}
            onClear={() => {
                onPatch({
                    counterpartyMode: 'existing',
                    counterpartyId: null,
                    newCounterpartyName: '',
                });
            }}
            onCreate={typed => {
                onPatch({
                    counterpartyMode: 'new',
                    counterpartyId: null,
                    newCounterpartyName: typed,
                });
            }}
            noneLabel="── None (self-transfer)"
            createLabel={typed => `+ Create '${typed}'`}
            placeholder="Pick counterparty…"
            disabled={disabled}
            ariaLabel="Counterparty"
        />
    );
}

function AccountPicker({
    draft,
    items,
    onPatch,
    disabled,
}: {
    draft: RowDraft;
    items: ComboboxItem<AccountId>[];
    onPatch: (patch: Partial<RowDraft>) => void;
    disabled: boolean;
}) {
    return (
        <Combobox
            items={items}
            value={draft.accountId}
            onChange={id => {
                onPatch({ accountId: id });
            }}
            groupOrder={ACCOUNT_TYPE_ORDER}
            groupLabels={ACCOUNT_TYPE_LABEL}
            placeholder="Pick account…"
            disabled={disabled}
            ariaLabel="Account"
        />
    );
}

function InboxRowActions({
    bankTransaction,
    pristine,
    disabled,
    onReset,
    onDismiss,
}: {
    bankTransaction: BankTransaction;
    pristine: boolean;
    disabled: boolean;
    onReset: () => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    const attach = useAttachBankTransaction();
    const toast = useToast();
    const hint = bankTransaction.matchingJournalEntry;

    async function onAttachClick() {
        if (!hint) return;
        try {
            await attach.mutateAsync({
                id: bankTransaction.id,
                journalEntryId: hint.id,
            });
            toast.success(`Attached to ${hint.otherAccountName}.`);
        } catch (err) {
            if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <div className="flex items-center justify-end gap-1 pt-1">
            {hint && (
                <button
                    type="button"
                    onClick={() => void onAttachClick()}
                    disabled={disabled || attach.isPending}
                    aria-label={`Attach to ${hint.otherAccountName}`}
                    title={`Attach to JE on ${hint.date} (${hint.otherAccountName})`}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-brand-primary hover:bg-brand-primary-soft disabled:opacity-60"
                >
                    <Icon name="link" size={14} strokeWidth={2} />
                    Attach
                </button>
            )}
            <Link
                to="/bank-transactions/$id/categorize"
                params={{ id: bankTransaction.id }}
                aria-label="Edit details"
                title="Edit details (splits, custom date)"
                className="inline-flex items-center justify-center p-1 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="pencil" size={14} strokeWidth={2} />
            </Link>
            <button
                type="button"
                onClick={onReset}
                disabled={pristine || disabled}
                aria-label="Reset draft"
                title="Reset draft to server suggestion"
                className="inline-flex items-center justify-center p-1 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2 disabled:opacity-40 disabled:cursor-not-allowed"
            >
                <Icon name="repeat" size={14} strokeWidth={2} />
            </button>
            <button
                type="button"
                onClick={() => {
                    onDismiss(bankTransaction);
                }}
                disabled={disabled}
                aria-label="Dismiss"
                title="Dismiss this row"
                className="inline-flex items-center justify-center p-1 rounded-sm text-fg-3 hover:text-danger hover:bg-surface-2 disabled:opacity-40"
            >
                <Icon name="x" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function UndismissButton({ bankTransaction }: { bankTransaction: BankTransaction }) {
    const undismiss = useUndismissBankTransaction();
    const toast = useToast();

    async function onClick() {
        try {
            await undismiss.mutateAsync(bankTransaction.id);
            toast.success('Restored to inbox.');
        } catch (err) {
            if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <div className="flex items-center justify-end">
            <button
                type="button"
                onClick={() => void onClick()}
                disabled={undismiss.isPending}
                aria-label="Undismiss"
                className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-fg-2 hover:text-fg-1 hover:bg-surface-2 disabled:opacity-60"
            >
                <Icon name="inbox" size={14} strokeWidth={2} />
                Undismiss
            </button>
        </div>
    );
}

function DismissDialog({
    bankTransaction,
    onClose,
}: {
    bankTransaction: BankTransaction;
    onClose: () => void;
}) {
    const dismiss = useDismissBankTransaction();
    const toast = useToast();
    const [reason, setReason] = useState('');
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        try {
            await dismiss.mutateAsync({ id: bankTransaction.id, reason });
            toast.success('Dismissed.');
            onClose();
        } catch (err) {
            if (err instanceof ApiError) {
                if (err.fieldErrors) {
                    setFieldErrors(err.fieldErrors);
                } else if (err.status >= 400 && err.status < 500) {
                    setTopError(err.message);
                } else {
                    toast.error(err.message);
                }
            } else if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <Modal
            open
            onClose={onClose}
            title="Dismiss bank transaction"
            description="Mark this row as not needing a journal entry. You can undismiss later."
            width="sm"
        >
            <form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                noValidate
            >
                <FormErrorBanner message={topError} />
                <label className="flex flex-col gap-1">
                    <span className="text-[12px] font-medium text-fg-2">Reason</span>
                    <textarea
                        value={reason}
                        onChange={e => {
                            setReason(e.target.value);
                        }}
                        required
                        maxLength={500}
                        rows={3}
                        autoFocus
                        placeholder="e.g. settled by journal entry X"
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong resize-none"
                    />
                    <FieldError name="Reason" errors={fieldErrors} />
                </label>
                <ModalFooter>
                    <button
                        type="button"
                        onClick={onClose}
                        disabled={dismiss.isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={dismiss.isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {dismiss.isPending ? 'Dismissing…' : 'Dismiss'}
                    </button>
                </ModalFooter>
            </form>
        </Modal>
    );
}

function BulkDismissDialog({
    selectionCount,
    onClose,
    onConfirm,
}: {
    selectionCount: number;
    onClose: () => void;
    onConfirm: (reason: string) => void;
}) {
    const [reason, setReason] = useState('');
    const trimmed = reason.trim();
    const canSubmit = trimmed.length > 0;

    function submit() {
        if (!canSubmit) return;
        onConfirm(trimmed);
    }

    return (
        <Modal
            open
            onClose={onClose}
            title={`Dismiss ${selectionCount.toString()} bank transaction${selectionCount === 1 ? '' : 's'}`}
            description="Stage these rows for dismissal. Save-all to commit; until then you can review or reset per row."
            width="sm"
        >
            <form
                onSubmit={e => {
                    e.preventDefault();
                    submit();
                }}
                noValidate
            >
                <label className="flex flex-col gap-1">
                    <span className="text-[12px] font-medium text-fg-2">Reason</span>
                    <textarea
                        value={reason}
                        onChange={e => {
                            setReason(e.target.value);
                        }}
                        required
                        maxLength={500}
                        rows={3}
                        autoFocus
                        placeholder="e.g. fee corrections, self-transfer siblings"
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong resize-none"
                    />
                </label>
                <ModalFooter>
                    <button
                        type="button"
                        onClick={onClose}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={!canSubmit}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        Dismiss {selectionCount.toString()} row{selectionCount === 1 ? '' : 's'}
                    </button>
                </ModalFooter>
            </form>
        </Modal>
    );
}

function formatSaveAllToast(summary: SaveAllSummary): string {
    return `${summary.categorised.toString()} categorised, ${summary.dismissed.toString()} dismissed, ${summary.failed.toString()} failed.`;
}
