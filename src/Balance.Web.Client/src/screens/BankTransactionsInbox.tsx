import { useMemo, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { plural, t } from '@lingui/core/macro';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import { useAccounts, type Account } from '../api/accounts';
import { useBankAccounts, type BankAccount } from '../api/bankAccounts';
import {
    BANK_TRANSACTION_FILTERS,
    useAttachBankTransaction,
    useBankTransactions,
    useDismissBankTransaction,
    useUndismissBankTransaction,
    type BankTransaction,
    type BankTransactionFilter,
} from '../api/bankTransactions';
import { useCounterparties, type Counterparty } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { AccountSelect } from '../components/AccountSelect';
import { ComboBox } from '../components/ui/ComboBox';
import { type ComboBoxItem } from '../components/ui/combobox.state';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Form } from 'react-aria-components';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { SelectionCheckbox } from '../components/ui/SelectionCheckbox';
import { TextField } from '../components/ui/TextField';
import { SearchField } from '../components/ui/SearchField';
import { selectedKey } from '../components/ui/selection';
import { Tag, TagGroup } from '../components/ui/TagGroup';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/ui/Toast';
import { cx } from '../lib/cx';
import { formatTableDate } from '../lib/dates';
import { type AccountId, type CounterpartyId } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';
import { formatMoney } from '../lib/money';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import {
    allVisibleSelectionState,
    rowStatus,
    type AllVisibleSelectionState,
    type RowDraft,
    type RowStatus,
} from './bankTransactionsInbox.state';
import { useInboxEditor, type ActionBarProps } from './bankTransactionsInbox.hooks';

const PAGE_SIZE = 50;

function filterLabel(filter: BankTransactionFilter): string {
    const labels: Record<BankTransactionFilter, string> = {
        Inbox: t`Inbox`,
        Matched: t`Matched`,
        Dismissed: t`Dismissed`,
        All: t`All`,
    };
    return labels[filter];
}

function subtitleFor(filter: BankTransactionFilter): string {
    const subtitles: Record<BankTransactionFilter, string> = {
        Inbox: t`Bank rows waiting for a journal entry. Pick Counterparty and Account inline; Save all when you’re happy.`,
        Matched: t`Bank rows that have been categorized into a journal entry.`,
        Dismissed: t`Bank rows you marked as not needing a journal entry.`,
        All: t`Every imported bank row, regardless of state.`,
    };
    return subtitles[filter];
}

function emptyTitleFor(filter: BankTransactionFilter): string {
    const titles: Record<BankTransactionFilter, string> = {
        Inbox: t`You're caught up.`,
        Matched: t`Nothing categorized yet.`,
        Dismissed: t`Nothing dismissed.`,
        All: t`No bank transactions yet.`,
    };
    return titles[filter];
}

function emptyHintFor(filter: BankTransactionFilter): string {
    const hints: Record<BankTransactionFilter, string> = {
        Inbox: t`Imported rows that need categorizing will appear here.`,
        Matched: t`Categorize an inbox row to see it here.`,
        Dismissed: t`Dismissed rows live here for audit.`,
        All: t`Import a bank statement from Bank imports to get started.`,
    };
    return hints[filter];
}

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
    const { t } = useLingui();
    const skip = (page - 1) * PAGE_SIZE;
    const debouncedQ = useDebouncedValue(q, 200);
    const query = useBankTransactions(skip, PAGE_SIZE, filter, debouncedQ);
    const catalog = useCurrencyCatalog();
    const [dismissing, setDismissing] = useState<BankTransaction | null>(null);

    return (
        <>
            <Panel>
                <SectionHead title={t`Bank transactions`} subtitle={subtitleFor(filter)} />
                <div className="flex flex-col gap-3 mb-4">
                    <FilterChips value={filter} onChange={onFilterChange} />
                    <SearchField
                        aria-label={t`Search bank transactions`}
                        value={q}
                        onChange={onSearchChange}
                        placeholder={t`Search description or counterparty…`}
                    />
                </div>
                <Body
                    query={query}
                    catalog={catalog}
                    filter={filter}
                    page={page}
                    search={debouncedQ}
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
    const { t } = useLingui();
    return (
        <TagGroup
            aria-label={t`Filter`}
            selectionMode="single"
            disallowEmptySelection
            selectedKeys={[value]}
            onSelectionChange={keys => {
                const next = selectedKey(keys);
                if (next !== undefined) onChange(next as BankTransactionFilter);
            }}
        >
            {BANK_TRANSACTION_FILTERS.map(filter => (
                <Tag key={filter} id={filter} shape="chip">
                    {filterLabel(filter)}
                </Tag>
            ))}
        </TagGroup>
    );
}

function Body({
    query,
    catalog,
    filter,
    page,
    search,
    onPageChange,
    onDismiss,
}: {
    query: ReturnType<typeof useBankTransactions>;
    catalog: CurrencyCatalog;
    filter: BankTransactionFilter;
    page: number;
    search: string;
    onPageChange: (page: number) => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    const { t } = useLingui();
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
                message={t`Couldn't load bank transactions.`}
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.items.length === 0 && search !== '') {
        return (
            <div className="py-8 text-center text-sm text-fg-2">
                <Trans>No matches for “{search}”.</Trans>
            </div>
        );
    }

    if (query.data.items.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-sm text-fg-2">{emptyTitleFor(filter)}</span>
                <span className="text-xs text-fg-3">{emptyHintFor(filter)}</span>
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
            <div className="hidden lg:grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px_minmax(180px,200px)] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>
                    <Trans>Date</Trans>
                </span>
                <span>
                    <Trans>Description</Trans>
                </span>
                <span>
                    <Trans>Counterparty</Trans>
                </span>
                <span className="text-right">
                    <Trans>Amount</Trans>
                </span>
                <span className="text-right">
                    <Trans>Actions</Trans>
                </span>
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
                <span className="text-xs text-fg-3 tabular-nums">
                    {formatTableDate(bankTransaction.bookingDate)}
                </span>
                <div className="min-w-0 flex flex-col leading-tight">
                    <span className="text-sm text-fg-1 truncate">
                        {bankTransaction.description}
                    </span>
                    <ReferenceLine reference={bankTransaction.reference} />
                    <StateChip bankTransaction={bankTransaction} />
                </div>
                <CounterpartyCell bankTransaction={bankTransaction} />
                <AmountCell bankTransaction={bankTransaction} catalog={catalog} />
                <ReadOnlyActions bankTransaction={bankTransaction} onDismiss={onDismiss} />
            </div>
            <div className="lg:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-center justify-between gap-3">
                    <span className="text-xs text-fg-3 tabular-nums">
                        {bankTransaction.bookingDate}
                    </span>
                    <AmountCell bankTransaction={bankTransaction} catalog={catalog} />
                </div>
                <span className="text-sm text-fg-1 truncate">{bankTransaction.description}</span>
                <ReferenceLine reference={bankTransaction.reference} />
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
        return (
            <span className="text-xs text-success tabular-nums">
                <Trans>Categorized</Trans>
            </span>
        );
    }
    if (bankTransaction.dismissedAt) {
        const reason = bankTransaction.dismissedReason ?? '';
        return (
            <span className="text-xs text-fg-3 tabular-nums truncate">
                <Trans>Dismissed</Trans>
                {reason ? ` · ${reason}` : ''}
            </span>
        );
    }
    return null;
}

function CounterpartyCell({ bankTransaction }: { bankTransaction: BankTransaction }) {
    const name = bankTransaction.counterpartyName;
    const iban = bankTransaction.counterpartyAccountNumber;
    if (!name && !iban) {
        return <span className="text-xs text-fg-3">—</span>;
    }
    return (
        <div className="min-w-0 flex flex-col leading-tight">
            <span className="text-xs text-fg-2 truncate" title={name ?? undefined}>
                {name ?? '—'}
            </span>
            {iban && (
                <span className="text-xs text-fg-3 truncate tabular-nums" title={iban}>
                    {iban}
                </span>
            )}
        </div>
    );
}

/** The bank-supplied payment reference, rendered as a muted sub-line under the
 *  description. Truncates to one line with the full value in a hover tooltip —
 *  bank references can be long, structured SEPA blobs. */
function ReferenceLine({ reference }: { reference: string | null }) {
    if (!reference) return null;
    return (
        <span className="text-xs text-fg-3 truncate" title={reference}>
            <Trans>Ref: {reference}</Trans>
        </span>
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
    const color = money.amount < 0 ? 'text-danger' : 'text-success';
    return (
        <span className={cx('font-mono text-sm tabular-nums text-right', color)}>
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
    const { t } = useLingui();
    if (bankTransaction.journalEntryId) {
        return (
            <div className="flex items-center justify-end">
                <Link
                    to="/journal/$id"
                    params={{ id: bankTransaction.journalEntryId }}
                    aria-label={t`View journal entry`}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-brand-primary hover:bg-brand-primary-soft"
                >
                    <Icon name="book-open" size={14} strokeWidth={2} />
                    <Trans>View journal entry</Trans>
                </Link>
            </div>
        );
    }
    if (bankTransaction.dismissedAt) {
        return <UndismissButton bankTransaction={bankTransaction} />;
    }
    return (
        <div className="flex items-center justify-end gap-1">
            <Link
                to="/bank-transactions/$id/categorize"
                params={{ id: bankTransaction.id }}
                aria-label={t`Categorize`}
                className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-brand-primary hover:bg-brand-primary-soft"
            >
                <Icon name="check-circle" size={14} strokeWidth={2} />
                <Trans>Categorize</Trans>
            </Link>
            <button
                type="button"
                onClick={() => {
                    onDismiss(bankTransaction);
                }}
                aria-label={t`Dismiss`}
                className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-fg-2 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="x" size={14} strokeWidth={2} />
                <Trans>Dismiss</Trans>
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
    const { t } = useLingui();
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
            <ErrorState
                message={t`Couldn't load accounts.`}
                onRetry={() => void accounts.refetch()}
            />
        );
    }
    if (counterparties.isError) {
        return (
            <ErrorState
                message={t`Couldn't load counterparties.`}
                onRetry={() => void counterparties.refetch()}
            />
        );
    }
    if (bankAccounts.isError) {
        return (
            <ErrorState
                message={t`Couldn't load bank accounts.`}
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
    const { t } = useLingui();
    const {
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
        onRowCheckboxClick,
        onHeaderCheckboxClick,
        applyBulkDismiss,
        discardAll,
    } = useInboxEditor({ bankTransactions, accounts, counterparties, bankAccounts });

    return (
        <div className="flex flex-col">
            <ActionBar {...actionBarProps} />
            <div className="hidden lg:flex flex-col">
                <div className="grid grid-cols-[28px_88px_1fr_minmax(180px,1.4fr)_minmax(180px,1.4fr)_120px_120px] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                    <HeaderSelectAllCheckbox
                        state={allVisibleSelectionState(selection, visibleIds)}
                        onClick={onHeaderCheckboxClick}
                        disabled={saving || visibleIds.length === 0}
                    />
                    <span>
                        <Trans>Date</Trans>
                    </span>
                    <span>
                        <Trans>Description</Trans>
                    </span>
                    <span>
                        <Trans>Counterparty</Trans>
                    </span>
                    <span>
                        <Trans>Account</Trans>
                    </span>
                    <span className="text-right">
                        <Trans>Amount</Trans>
                    </span>
                    <span className="text-right">
                        <Trans>Actions</Trans>
                    </span>
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
                            currencyCode={bt.money.currencyCode}
                            excludeAccountId={
                                bankAccountsById.get(bt.bankAccountId)?.accountId ?? null
                            }
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
                title={t`Discard unsaved drafts?`}
                message={
                    dirtyCount > 0
                        ? t`Reset all ${dirtyCount.toString()} unsaved drafts back to the server-suggested values.`
                        : undefined
                }
                confirmLabel={t`Discard`}
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
                    title={t`Leave with unsaved drafts?`}
                    message={t`You have ${plural(dirtyCount, {
                        one: '# unsaved draft',
                        other: '# unsaved drafts',
                    })}. Leaving will discard them.`}
                    confirmLabel={t`Leave`}
                    variant="destructive"
                />
            )}
        </div>
    );
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
    return (
        <SelectionCheckbox
            aria-label={ariaLabel}
            isSelected={selected}
            isDisabled={disabled}
            onChange={({ shiftKey }) => {
                onClick(shiftKey);
            }}
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
    const { t } = useLingui();
    return (
        <SelectionCheckbox
            aria-label={t`Select all visible rows`}
            isSelected={state === 'all'}
            isIndeterminate={state === 'some'}
            isDisabled={disabled}
            onChange={onClick}
        />
    );
}

/**
 * Combined selection-actions + save-controls bar, rendered inline above the
 * inbox list. Selection row shows when there are selected rows; save row
 * shows when there are unsaved drafts or a save is in flight. Either or both
 * may render, with a divider between.
 *
 * Picker values (`bulkCounterparty`, `bulkAccountId`) are owned by the parent
 * so they survive Apply, Clear, and pagination — re-applying the same
 * counterparty + account across multiple pages of similar rows is the common
 * categorization flow.
 */
function ActionBar({
    selectionCount,
    selectedCurrencies,
    bulkCounterparty,
    bulkAccountId,
    counterpartyItems,
    bulkCurrency,
    excludeAccountIds,
    saving,
    progress,
    dirtyCount,
    readyCount,
    onBulkCounterpartyChange,
    onBulkAccountIdChange,
    onApply,
    onApplySuggestions,
    onBulkDismiss,
    onClearSelection,
    onSave,
    onDiscard,
}: ActionBarProps) {
    const { t } = useLingui();
    const showSelection = selectionCount > 0;
    const showSave = dirtyCount > 0 || saving;

    const mixedCurrency = selectedCurrencies.length > 1;
    const accountDisabled = saving || mixedCurrency;
    const canApply = !saving && (bulkCounterparty !== null || bulkAccountId !== null);

    const cpValue: CounterpartyId | null =
        bulkCounterparty?.kind === 'existing' ? bulkCounterparty.counterpartyId : null;
    const cpItemsWithPending = useMemo(() => {
        if (bulkCounterparty?.kind !== 'new' || bulkCounterparty.name.trim().length === 0) {
            return counterpartyItems;
        }
        const pending: ComboBoxItem<CounterpartyId | null> = {
            key: '__pending_bulk__',
            label: t`${bulkCounterparty.name.trim()} (new)`,
            value: null,
        };
        return [pending, ...counterpartyItems];
    }, [bulkCounterparty, counterpartyItems, t]);

    if (!showSelection && !showSave) return null;

    return (
        <div className="hidden lg:block mb-3 rounded-lg bg-brand-primary-soft border border-brand-primary/30">
            {showSelection && (
                <div className="px-3 py-2">
                    <div className="flex flex-wrap items-center gap-3">
                        <span className="text-xs font-medium text-fg-1">
                            <Trans>{selectionCount.toString()} selected</Trans>
                        </span>
                        <div className="min-w-[180px] flex-1 max-w-[260px]">
                            <ComboBox
                                items={cpItemsWithPending}
                                value={cpValue}
                                onChange={id => {
                                    onBulkCounterpartyChange({
                                        kind: 'existing',
                                        counterpartyId: id,
                                    });
                                }}
                                onClear={() => {
                                    onBulkCounterpartyChange({
                                        kind: 'existing',
                                        counterpartyId: null,
                                    });
                                }}
                                onCreate={typed => {
                                    onBulkCounterpartyChange({ kind: 'new', name: typed });
                                }}
                                noneLabel={t`── None (self-transfer)`}
                                placeholder={t`Counterparty…`}
                                disabled={saving}
                                ariaLabel={t`Bulk counterparty`}
                            />
                        </div>
                        <div className="min-w-[180px] flex-1 max-w-[260px]">
                            <AccountSelect
                                value={bulkAccountId}
                                onChange={id => {
                                    onBulkAccountIdChange(id);
                                }}
                                postableOnly
                                currencyCode={bulkCurrency ?? undefined}
                                exclude={[...excludeAccountIds]}
                                placeholder={
                                    mixedCurrency ? t`Account (mixed currencies)` : t`Account…`
                                }
                                disabled={accountDisabled}
                                ariaLabel={t`Bulk account`}
                            />
                        </div>
                        <button
                            type="button"
                            onClick={onApply}
                            disabled={!canApply}
                            className="px-3 py-[7px] rounded-lg text-sm font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                        >
                            <Trans>Apply to {selectionCount.toString()} selected</Trans>
                        </button>
                        <button
                            type="button"
                            onClick={onApplySuggestions}
                            disabled={saving}
                            title={t`Fill the selected rows with the IBAN-matched counterparty and the last-used account for that counterparty.`}
                            className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-1 border border-border-strong hover:bg-surface-2 disabled:opacity-60"
                        >
                            <Trans>Apply suggestions</Trans>
                        </button>
                        <button
                            type="button"
                            onClick={onBulkDismiss}
                            disabled={saving}
                            className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-1 border border-border-strong hover:bg-surface-2 disabled:opacity-60"
                        >
                            <Trans>Dismiss with reason…</Trans>
                        </button>
                        <button
                            type="button"
                            onClick={onClearSelection}
                            disabled={saving}
                            className="px-2 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                        >
                            <Trans>Clear</Trans>
                        </button>
                    </div>
                    {mixedCurrency && (
                        <p className="mt-1 text-xs text-fg-3">
                            <Trans>
                                Selected rows span {selectedCurrencies.join(' + ')} - Account
                                can&apos;t be bulk-applied.
                            </Trans>
                        </p>
                    )}
                </div>
            )}
            {showSelection && showSave && <div className="border-t border-border-soft" />}
            {showSave && (
                <div className="flex items-center justify-between gap-3 px-3 py-2">
                    <div className="flex items-center gap-3 text-xs text-fg-2">
                        {saving && progress ? (
                            <span className="tabular-nums">
                                <Trans>
                                    Saving {progress.done.toString()}/{progress.total.toString()}…
                                </Trans>
                            </span>
                        ) : (
                            <span>
                                <Trans>
                                    {dirtyCount.toString()} unsaved · {readyCount.toString()} ready
                                </Trans>
                            </span>
                        )}
                    </div>
                    <div className="flex items-center gap-2">
                        <button
                            type="button"
                            onClick={onDiscard}
                            disabled={saving}
                            className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                        >
                            <Trans>Discard</Trans>
                        </button>
                        <button
                            type="button"
                            onClick={onSave}
                            disabled={saving || readyCount === 0}
                            className="px-3 py-[7px] rounded-lg text-sm font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                        >
                            {saving ? (
                                t`Saving…`
                            ) : (
                                <Plural value={readyCount} one="Save # row" other="Save # rows" />
                            )}
                        </button>
                    </div>
                </div>
            )}
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
    currencyCode,
    excludeAccountId,
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
    counterpartyItems: ComboBoxItem<CounterpartyId | null>[];
    currencyCode: string;
    excludeAccountId: AccountId | null;
    catalog: CurrencyCatalog;
    saving: boolean;
    selected: boolean;
    onCheckboxClick: (shiftKey: boolean) => void;
    onPatch: (patch: Partial<RowDraft>) => void;
    onReset: () => void;
    onDismiss: (bt: BankTransaction) => void;
}) {
    const { t } = useLingui();
    const status = rowStatus(draft);
    const willDismiss = dismissDraft !== null;

    return (
        <div className="grid grid-cols-[28px_88px_1fr_minmax(180px,1.4fr)_minmax(180px,1.4fr)_120px_120px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0">
            <div className="pt-2">
                <RowSelectCheckbox
                    selected={selected}
                    disabled={saving}
                    onClick={onCheckboxClick}
                    ariaLabel={t`Select bank transaction ${bankTransaction.description}`}
                />
            </div>
            <div className="flex flex-col leading-tight pt-2">
                <span className="text-xs text-fg-3 tabular-nums">
                    {formatTableDate(bankTransaction.bookingDate)}
                </span>
                {willDismiss ? <WillDismissIndicator /> : <StatusIndicator status={status} />}
            </div>
            <div className="min-w-0 flex flex-col leading-tight pt-2">
                <span className="text-sm text-fg-1 truncate" title={bankTransaction.description}>
                    {bankTransaction.description}
                </span>
                {bankTransaction.counterpartyName && (
                    <span
                        className="text-xs text-fg-3 truncate"
                        title={bankTransaction.counterpartyName}
                    >
                        {bankTransaction.counterpartyName}
                    </span>
                )}
                {bankTransaction.counterpartyAccountNumber && (
                    <span
                        className="text-xs text-fg-3 truncate tabular-nums"
                        title={bankTransaction.counterpartyAccountNumber}
                    >
                        {bankTransaction.counterpartyAccountNumber}
                    </span>
                )}
                <ReferenceLine reference={bankTransaction.reference} />
                {bankTransaction.matchingJournalEntry && (
                    <AttachHintBadge hint={bankTransaction.matchingJournalEntry} />
                )}
                {bankTransaction.loanPaymentHint && (
                    <LoanPaymentHintBadge
                        bankTransactionId={bankTransaction.id}
                        hint={bankTransaction.loanPaymentHint}
                    />
                )}
                {dismissDraft !== null && (
                    <span className="text-xs text-warning mt-1 truncate">
                        <Trans>Reason: {dismissDraft}</Trans>
                    </span>
                )}
                {error && <span className="text-xs text-danger mt-1">{error}</span>}
            </div>
            <CounterpartyPicker
                draft={draft}
                items={counterpartyItems}
                onPatch={onPatch}
                disabled={saving || willDismiss}
            />
            <AccountPicker
                draft={draft}
                currencyCode={currencyCode}
                excludeAccountId={excludeAccountId}
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
            <span className="text-xs text-success tabular-nums inline-flex items-center gap-1">
                <span aria-hidden>●</span> <Trans>ready</Trans>
            </span>
        );
    }
    if (status === 'invalid') {
        return (
            <span className="text-xs text-warning tabular-nums inline-flex items-center gap-1">
                <span aria-hidden>⚠</span> <Trans>invalid</Trans>
            </span>
        );
    }
    return (
        <span className="text-xs text-fg-3 tabular-nums inline-flex items-center gap-1">
            <span aria-hidden>—</span>
        </span>
    );
}

function AttachHintBadge({ hint }: { hint: NonNullable<BankTransaction['matchingJournalEntry']> }) {
    const { t } = useLingui();
    return (
        <span
            className="text-xs text-brand-primary mt-1 truncate inline-flex items-center gap-1"
            title={t`Auto-matched to journal entry on ${hint.date}`}
        >
            <Icon name="link" size={11} strokeWidth={2} />
            <Trans>Matches journal entry · {hint.otherAccountName}</Trans>
        </span>
    );
}

/** Loan-payment hint (ADR-0025): one click into the loan-aware categorize mode. */
function LoanPaymentHintBadge({
    bankTransactionId,
    hint,
}: {
    bankTransactionId: BankTransaction['id'];
    hint: NonNullable<BankTransaction['loanPaymentHint']>;
}) {
    const { t } = useLingui();
    return (
        <Link
            to="/bank-transactions/$id/categorize"
            params={{ id: bankTransactionId }}
            search={{ loan: hint.loanId }}
            className="text-xs text-brand-primary mt-1 truncate inline-flex items-center gap-1 hover:underline"
            title={t`Looks like a payment on ${hint.loanName}`}
        >
            <Icon name="landmark" size={11} strokeWidth={2} />
            <Trans>Loan payment · {hint.loanName}</Trans>
        </Link>
    );
}

function WillDismissIndicator() {
    return (
        <span className="text-xs text-warning tabular-nums inline-flex items-center gap-1">
            <span aria-hidden>●</span> <Trans>will dismiss</Trans>
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
    items: ComboBoxItem<CounterpartyId | null>[];
    onPatch: (patch: Partial<RowDraft>) => void;
    disabled: boolean;
}) {
    const { t } = useLingui();
    // Render the in-progress "new" name as a synthetic item, so the user sees
    // what they typed across renders.
    const effectiveItems = useMemo(() => {
        if (draft.counterpartyMode !== 'new' || draft.newCounterpartyName.trim().length === 0) {
            return items;
        }
        const pending: ComboBoxItem<CounterpartyId | null> = {
            key: '__pending__',
            label: t`${draft.newCounterpartyName.trim()} (new)`,
            value: null,
        };
        return [pending, ...items];
    }, [draft.counterpartyMode, draft.newCounterpartyName, items, t]);

    const value: CounterpartyId | null =
        draft.counterpartyMode === 'existing' ? draft.counterpartyId : null;

    return (
        <ComboBox
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
            noneLabel={t`── None (self-transfer)`}
            placeholder={t`Pick counterparty…`}
            disabled={disabled}
            ariaLabel={t`Counterparty`}
        />
    );
}

function AccountPicker({
    draft,
    currencyCode,
    excludeAccountId,
    onPatch,
    disabled,
}: {
    draft: RowDraft;
    currencyCode: string;
    excludeAccountId: AccountId | null;
    onPatch: (patch: Partial<RowDraft>) => void;
    disabled: boolean;
}) {
    const { t } = useLingui();
    return (
        <AccountSelect
            value={draft.accountId}
            onChange={id => {
                onPatch({ accountId: id });
            }}
            postableOnly
            currencyCode={currencyCode}
            exclude={excludeAccountId ? [excludeAccountId] : undefined}
            placeholder={t`Pick account…`}
            disabled={disabled}
            ariaLabel={t`Account`}
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
    const { t } = useLingui();
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
            toast.success(t`Attached to ${hint.otherAccountName}.`);
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
                    aria-label={t`Attach to ${hint.otherAccountName}`}
                    title={t`Attach to journal entry on ${hint.date} (${hint.otherAccountName})`}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-brand-primary hover:bg-brand-primary-soft disabled:opacity-60"
                >
                    <Icon name="link" size={14} strokeWidth={2} />
                    <Trans>Attach</Trans>
                </button>
            )}
            <Link
                to="/bank-transactions/$id/categorize"
                params={{ id: bankTransaction.id }}
                aria-label={t`Edit details`}
                title={t`Edit details (splits, custom date)`}
                className="inline-flex items-center justify-center p-1 rounded-lg text-fg-3 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="pencil" size={14} strokeWidth={2} />
            </Link>
            <button
                type="button"
                onClick={onReset}
                disabled={pristine || disabled}
                aria-label={t`Reset draft`}
                title={t`Reset draft to server suggestion`}
                className="inline-flex items-center justify-center p-1 rounded-lg text-fg-3 hover:text-fg-1 hover:bg-surface-2 disabled:opacity-40 disabled:cursor-not-allowed"
            >
                <Icon name="repeat" size={14} strokeWidth={2} />
            </button>
            <button
                type="button"
                onClick={() => {
                    onDismiss(bankTransaction);
                }}
                disabled={disabled}
                aria-label={t`Dismiss`}
                title={t`Dismiss this row`}
                className="inline-flex items-center justify-center p-1 rounded-lg text-fg-3 hover:text-danger hover:bg-surface-2 disabled:opacity-40"
            >
                <Icon name="x" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function UndismissButton({ bankTransaction }: { bankTransaction: BankTransaction }) {
    const { t } = useLingui();
    const undismiss = useUndismissBankTransaction();
    const toast = useToast();

    async function onClick() {
        try {
            await undismiss.mutateAsync(bankTransaction.id);
            toast.success(t`Restored to inbox.`);
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
                aria-label={t`Undismiss`}
                className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-fg-2 hover:text-fg-1 hover:bg-surface-2 disabled:opacity-60"
            >
                <Icon name="inbox" size={14} strokeWidth={2} />
                <Trans>Undismiss</Trans>
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
    const { t } = useLingui();
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
            toast.success(t`Dismissed.`);
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal
            open
            onClose={onClose}
            title={t`Dismiss bank transaction`}
            description={t`Mark this row as not needing a journal entry. You can undismiss later.`}
            width="sm"
        >
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <TextField
                    multiline
                    rows={3}
                    name="Reason"
                    label={t`Reason`}
                    value={reason}
                    onChange={setReason}
                    isRequired
                    maxLength={500}
                    autoFocus
                    placeholder={t`e.g. settled by journal entry X`}
                />
                <ModalFooter>
                    <button
                        type="button"
                        onClick={onClose}
                        disabled={dismiss.isPending}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        <Trans>Cancel</Trans>
                    </button>
                    <button
                        type="submit"
                        disabled={dismiss.isPending}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {dismiss.isPending ? t`Dismissing…` : t`Dismiss`}
                    </button>
                </ModalFooter>
            </Form>
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
    const { t } = useLingui();
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
            title={t`Dismiss ${plural(selectionCount, {
                one: '# bank transaction',
                other: '# bank transactions',
            })}`}
            description={t`Stage these rows for dismissal. Save-all to commit; until then you can review or reset per row.`}
            width="sm"
        >
            <Form
                onSubmit={e => {
                    e.preventDefault();
                    submit();
                }}
            >
                <TextField
                    multiline
                    rows={3}
                    label={t`Reason`}
                    value={reason}
                    onChange={setReason}
                    isRequired
                    maxLength={500}
                    autoFocus
                    placeholder={t`e.g. fee corrections, self-transfer siblings`}
                />
                <ModalFooter>
                    <button
                        type="button"
                        onClick={onClose}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1"
                    >
                        <Trans>Cancel</Trans>
                    </button>
                    <button
                        type="submit"
                        disabled={!canSubmit}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        <Plural value={selectionCount} one="Dismiss # row" other="Dismiss # rows" />
                    </button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}
