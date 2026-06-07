import { useMemo, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccount, useAccounts, useDeleteAccount, type Account } from '../api/accounts';
import { useCurrencyCatalog } from '../api/currencies';
import { useReassignJournalLines } from '../api/journalLines';
import {
    useAccountRegister,
    type RegisterFilters,
    type RegisterRow,
    type RegisterStatusFilter,
} from '../api/register';
import { AccountAvatar } from '../components/AccountAvatar';
import { AccountSelect } from '../components/AccountSelect';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DateRangePicker } from '../components/ui/DateRangePicker';
import { SearchField } from '../components/ui/SearchField';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { selectedKey } from '../components/ui/selection';
import { Tag, TagGroup } from '../components/ui/TagGroup';
import { useToast } from '../components/ui/Toast';
import { accountPathLabel } from '../lib/accountTree';
import { cx } from '../lib/cx';
import { type AccountId, type JournalLineId } from '../lib/domain';
import { handleActionError } from '../lib/formErrors';
import { formatMoney } from '../lib/money';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import { AccountFormModal } from './AccountForm';
import { LinkedBankAccountsSection } from './LinkedBankAccounts';

const REGISTER_PAGE_SIZE = 50;

/** The register's URL-backed filters, minus the separately-debounced `q`. */
export type RegisterFilterState = {
    posted: AccountId | null;
    counter: AccountId | null;
    from: string;
    to: string;
    status: RegisterStatusFilter;
};

export function AccountDetail({
    id,
    page,
    q,
    filters,
    onPageChange,
    onSearchChange,
    onFiltersChange,
}: {
    id: AccountId;
    page: number;
    q: string;
    filters: RegisterFilterState;
    onPageChange: (p: number) => void;
    onSearchChange: (q: string) => void;
    onFiltersChange: (patch: Partial<RegisterFilterState>) => void;
}) {
    const query = useAccount(id);
    const [editing, setEditing] = useState(false);
    const [deleting, setDeleting] = useState(false);

    if (query.isPending) {
        return (
            <Panel>
                <Skeleton className="h-6 w-1/3 mb-3" />
                <Skeleton className="h-4 w-1/2" />
            </Panel>
        );
    }

    if (query.isError) {
        return (
            <Panel>
                <ErrorState message="Couldn't load account." onRetry={() => void query.refetch()} />
            </Panel>
        );
    }

    const account = query.data;

    return (
        <>
            <Panel>
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="flex items-center gap-3 min-w-0">
                        <AccountAvatar account={account} size="md" />
                        <div className="flex flex-col gap-[2px] min-w-0">
                            <Link to="/accounts" className="text-12 text-fg-3 hover:text-fg-1">
                                ← Accounts
                            </Link>
                            <h1 className="text-22 font-medium text-fg-1 truncate">
                                {account.name}
                            </h1>
                            <span className="text-12 text-fg-3">
                                {account.type} · {account.currencyCode}
                            </span>
                        </div>
                    </div>
                    <div className="flex flex-col items-start gap-3 lg:flex-row lg:items-center lg:justify-between lg:shrink-0">
                        <Amount
                            minor={account.balance.amount}
                            currencyCode={account.balance.currencyCode}
                            size="big"
                            className={account.balance.amount < 0 ? 'text-danger' : ''}
                        />
                        <div className="flex items-center gap-2">
                            <button
                                type="button"
                                onClick={() => {
                                    setEditing(true);
                                }}
                                className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-13 font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                            >
                                <Icon name="pencil" size={14} strokeWidth={2} />
                                Edit
                            </button>
                            <button
                                type="button"
                                onClick={() => {
                                    setDeleting(true);
                                }}
                                className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-13 font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
                            >
                                <Icon name="trash" size={14} strokeWidth={2} />
                                Delete
                            </button>
                        </div>
                    </div>
                </div>
            </Panel>

            <Panel>
                <SectionHead title="Linked bank account" />
                <LinkedBankAccountsSection owner={{ kind: 'account', id: account.id }} />
            </Panel>

            <Panel>
                <SectionHead title="Register" subtitle="Chronological activity on this account." />
                <div className="mb-4 flex flex-col gap-3">
                    <SearchField
                        aria-label="Search register"
                        value={q}
                        onChange={onSearchChange}
                        placeholder="Search description or counterparty…"
                    />
                    <RegisterFilterBar
                        account={account}
                        filters={filters}
                        onFiltersChange={onFiltersChange}
                    />
                </div>
                <RegisterTable
                    account={account}
                    page={page}
                    q={q}
                    filters={filters}
                    onPageChange={onPageChange}
                />
            </Panel>

            {editing && (
                <AccountFormModal
                    mode="edit"
                    account={account}
                    onClose={() => {
                        setEditing(false);
                    }}
                />
            )}
            {deleting && (
                <DeleteAccountDialog
                    account={account}
                    onClose={() => {
                        setDeleting(false);
                    }}
                />
            )}
        </>
    );
}

const STATUS_FILTERS: readonly RegisterStatusFilter[] = ['', 'Uncleared', 'Cleared', 'Reconciled'];

const STATUS_LABEL: Record<RegisterStatusFilter, string> = {
    '': 'All',
    Uncleared: 'Uncleared',
    Cleared: 'Cleared',
    Reconciled: 'Reconciled',
};

function RegisterFilterBar({
    account,
    filters,
    onFiltersChange,
}: {
    account: Account;
    filters: RegisterFilterState;
    onFiltersChange: (patch: Partial<RegisterFilterState>) => void;
}) {
    return (
        <div className="flex flex-wrap items-center gap-2">
            {!account.isPostable && (
                <div className="w-64">
                    {/* The posted picker offers only the viewed subtree (anything else matches
                     *  nothing); picking a non-postable child means "that child's whole subtree"
                     *  (ADR-0019), so placeholders stay selectable here. */}
                    <AccountSelect
                        value={filters.posted}
                        subtreeOf={account.id}
                        onChange={v => {
                            onFiltersChange({ posted: v });
                        }}
                        onClear={() => {
                            onFiltersChange({ posted: null });
                        }}
                        noneLabel="Any sub-account"
                        placeholder="Sub-account…"
                        ariaLabel="Filter by sub-account"
                    />
                </div>
            )}
            <div className="w-64">
                <AccountSelect
                    value={filters.counter}
                    onChange={v => {
                        onFiltersChange({ counter: v });
                    }}
                    onClear={() => {
                        onFiltersChange({ counter: null });
                    }}
                    noneLabel="Any counter-account"
                    placeholder="Counter-account…"
                    ariaLabel="Filter by counter-account"
                />
            </div>
            <DateRangePicker
                aria-label="Date range"
                value={{ from: filters.from, to: filters.to }}
                onChange={range => {
                    onFiltersChange({ from: range.from, to: range.to });
                }}
                fieldClassName="text-12 py-[5px]"
            />
            <TagGroup
                aria-label="Status filter"
                selectionMode="single"
                disallowEmptySelection
                selectedKeys={[filters.status === '' ? 'all' : filters.status]}
                onSelectionChange={keys => {
                    const next = selectedKey(keys);
                    if (next === undefined) return;
                    onFiltersChange({
                        status: (next === 'all' ? '' : next) as RegisterStatusFilter,
                    });
                }}
            >
                {STATUS_FILTERS.map(status => (
                    <Tag
                        key={status === '' ? 'all' : status}
                        id={status === '' ? 'all' : status}
                        shape="chip"
                    >
                        {STATUS_LABEL[status]}
                    </Tag>
                ))}
            </TagGroup>
        </div>
    );
}

function RegisterTable({
    account,
    page,
    q,
    filters,
    onPageChange,
}: {
    account: Account;
    page: number;
    q: string;
    filters: RegisterFilterState;
    onPageChange: (p: number) => void;
}) {
    const skip = (page - 1) * REGISTER_PAGE_SIZE;
    const debouncedQ = useDebouncedValue(q, 200);
    const registerFilters: RegisterFilters = { q: debouncedQ, ...filters };
    const register = useAccountRegister(account.id, skip, REGISTER_PAGE_SIZE, registerFilters);
    const catalog = useCurrencyCatalog();
    const [selected, setSelected] = useState<ReadonlySet<JournalLineId>>(new Set());

    // Show the posted-account column only when rows can come from descendants — on a
    // leaf register every row would repeat the viewed account.
    const showAccountColumn = !account.isPostable;
    const gridClass = showAccountColumn
        ? 'grid-cols-[24px_100px_1fr_150px_160px_120px]'
        : 'grid-cols-[24px_100px_1fr_180px_120px]';

    const rows = useMemo(() => register.data?.items ?? [], [register.data]);
    // Selection is page-bound and prunes itself: ids that fell off the current page (or
    // stopped being movable) simply no longer count.
    const visibleSelected = useMemo(
        () =>
            new Set(
                rows
                    .filter(
                        r =>
                            selected.has(r.journalLineId) && r.reconciliationStatus === 'Uncleared',
                    )
                    .map(r => r.journalLineId),
            ),
        [rows, selected],
    );
    const selectableIds = useMemo(
        () => rows.filter(r => r.reconciliationStatus === 'Uncleared').map(r => r.journalLineId),
        [rows],
    );

    if (register.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-full" />
            </div>
        );
    }

    if (register.isError) {
        return (
            <ErrorState message="Couldn't load register." onRetry={() => void register.refetch()} />
        );
    }

    if (rows.length === 0) {
        const hasFilters =
            filters.posted !== null ||
            filters.counter !== null ||
            filters.from !== '' ||
            filters.to !== '' ||
            filters.status !== '';
        return (
            <div className="py-6 text-center text-13 text-fg-3">
                {debouncedQ !== ''
                    ? `No matches for “${debouncedQ}”.`
                    : hasFilters
                      ? 'No rows match the current filters.'
                      : 'No journal entries yet.'}
            </div>
        );
    }

    function toggleRow(id: JournalLineId) {
        setSelected(prev => {
            const next = new Set(prev);
            if (next.has(id)) {
                next.delete(id);
            } else {
                next.add(id);
            }
            return next;
        });
    }

    function toggleAll() {
        setSelected(
            visibleSelected.size === selectableIds.length ? new Set() : new Set(selectableIds),
        );
    }

    const allState: 'none' | 'some' | 'all' =
        visibleSelected.size === 0
            ? 'none'
            : visibleSelected.size === selectableIds.length
              ? 'all'
              : 'some';

    return (
        <div className="flex flex-col">
            {visibleSelected.size > 0 && (
                <ReassignBar
                    account={account}
                    selectedIds={[...visibleSelected]}
                    onDone={() => {
                        setSelected(new Set());
                    }}
                />
            )}
            <div
                className={cx(
                    'hidden lg:grid gap-3 px-2 pb-2 text-11 text-fg-3 uppercase tracking-wider border-b border-border-soft',
                    gridClass,
                )}
            >
                <span className="flex items-center">
                    <HeaderSelectAllCheckbox
                        state={allState}
                        disabled={selectableIds.length === 0}
                        onClick={toggleAll}
                    />
                </span>
                <span>Date</span>
                <span>Description</span>
                {showAccountColumn && <span>Account</span>}
                <span>Counter</span>
                <span className="text-right">Amount</span>
            </div>
            {rows.map(row => (
                <RegisterRowView
                    key={row.journalLineId}
                    row={row}
                    catalog={catalog}
                    gridClass={gridClass}
                    showAccountColumn={showAccountColumn}
                    selected={visibleSelected.has(row.journalLineId)}
                    onToggle={() => {
                        toggleRow(row.journalLineId);
                    }}
                />
            ))}
            <Pagination
                page={page}
                pageSize={REGISTER_PAGE_SIZE}
                totalCount={register.data.totalCount}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function ReassignBar({
    account,
    selectedIds,
    onDone,
}: {
    account: Account;
    selectedIds: readonly JournalLineId[];
    onDone: () => void;
}) {
    const accounts = useAccounts();
    const reassign = useReassignJournalLines();
    const toast = useToast();
    const [target, setTarget] = useState<AccountId | null>(null);
    const [confirming, setConfirming] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const byId = useMemo(() => new Map((accounts.data ?? []).map(a => [a.id, a])), [accounts.data]);
    const targetName = target !== null ? (accountPathLabel(byId, target) ?? '') : '';
    const count = selectedIds.length;

    async function onConfirm() {
        if (target === null) return;
        setError(null);
        try {
            await reassign.mutateAsync({ lineIds: selectedIds, targetAccountId: target });
            toast.success(
                `Moved ${String(count)} line${count === 1 ? '' : 's'} to “${targetName}”.`,
            );
            setConfirming(false);
            setTarget(null);
            onDone();
        } catch (err) {
            handleActionError(err, { setError, toast: toast.error });
        }
    }

    return (
        <div className="flex flex-wrap items-center gap-3 mb-3 px-3 py-2 rounded-sm bg-surface-2 border border-border-soft">
            <span className="text-13 text-fg-2 font-medium">
                {count} line{count === 1 ? '' : 's'} selected
            </span>
            <div className="w-72">
                {/* Reassign targets: postable accounts in the lines' currency (every line on
                 *  this register shares the viewed subtree's currency, ADR-0019). Cross-type
                 *  moves are legitimate (e.g. reclassifying an expense leg onto an asset). */}
                <AccountSelect
                    value={target}
                    onChange={setTarget}
                    postableOnly
                    currencyCode={account.currencyCode}
                    placeholder="Move to account…"
                    ariaLabel="Move selected lines to account"
                />
            </div>
            <button
                type="button"
                disabled={target === null || reassign.isPending}
                onClick={() => {
                    setConfirming(true);
                }}
                className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-13 font-medium hover:bg-brand-primary-dark disabled:opacity-50 disabled:cursor-not-allowed"
            >
                <Icon name="arrow-right" size={14} strokeWidth={2} />
                Move
            </button>
            <button
                type="button"
                onClick={onDone}
                className="px-3 py-[7px] rounded-sm text-13 font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
            >
                Clear selection
            </button>
            {confirming && (
                <ConfirmDialog
                    open
                    onClose={() => {
                        setConfirming(false);
                    }}
                    onConfirm={() => void onConfirm()}
                    title={`Move ${String(count)} line${count === 1 ? '' : 's'} to “${targetName}”?`}
                    message="Only the selected side of each entry moves — dates, amounts and the other side stay untouched. The whole batch moves together, or not at all."
                    confirmLabel="Move"
                    busy={reassign.isPending}
                    error={error}
                />
            )}
        </div>
    );
}

function RowSelectCheckbox({
    selected,
    disabled,
    onChange,
    ariaLabel,
}: {
    selected: boolean;
    disabled: boolean;
    onChange: () => void;
    ariaLabel: string;
}) {
    return (
        <input
            type="checkbox"
            aria-label={ariaLabel}
            title={disabled ? 'Cleared and reconciled lines can’t be moved.' : undefined}
            checked={selected}
            disabled={disabled}
            onChange={onChange}
            className="h-4 w-4 cursor-pointer accent-brand-primary disabled:opacity-40 disabled:cursor-not-allowed"
        />
    );
}

function HeaderSelectAllCheckbox({
    state,
    onClick,
    disabled,
}: {
    state: 'none' | 'some' | 'all';
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
            aria-label="Select all movable rows on this page"
            checked={state === 'all'}
            disabled={disabled}
            onChange={onClick}
            className="h-4 w-4 cursor-pointer accent-brand-primary disabled:opacity-40 disabled:cursor-not-allowed"
        />
    );
}

function RegisterRowView({
    row,
    catalog,
    gridClass,
    showAccountColumn,
    selected,
    onToggle,
}: {
    row: RegisterRow;
    catalog: ReturnType<typeof useCurrencyCatalog>;
    gridClass: string;
    showAccountColumn: boolean;
    selected: boolean;
    onToggle: () => void;
}) {
    const counter = row.counter[0];
    const extra = row.counter.length - 1;
    const negative = row.amount.amount < 0;
    const movable = row.reconciliationStatus === 'Uncleared';
    const heading = row.counterpartyName ?? row.entryDescription ?? '—';
    // Match the journal-list amount-coloring convention: in = success, out = danger.
    const amount = (
        <span
            className={cx(
                'font-mono text-13 tabular text-right',
                negative ? 'text-danger' : 'text-success',
            )}
        >
            {formatMoney(row.amount.amount, row.amount.currencyCode, catalog, { sign: true })}
        </span>
    );
    const counterLabel = (
        <span className="text-12 text-fg-2 truncate">
            {counter ? counter.accountName : '—'}
            {extra > 0 ? <span className="text-fg-3"> +{extra}</span> : null}
        </span>
    );
    const checkbox = (
        <RowSelectCheckbox
            selected={selected}
            disabled={!movable}
            onChange={onToggle}
            ariaLabel={`Select line of ${row.date}`}
        />
    );
    return (
        <div className="border-b border-border-soft last:border-b-0 hover:bg-surface-2">
            <div className={cx('hidden lg:grid gap-3 items-center px-2 py-2', gridClass)}>
                <span className="flex items-center">{checkbox}</span>
                <Link to="/journal/$id" params={{ id: row.journalEntryId }} className="contents">
                    <span className="text-12 text-fg-3 tabular">{row.date}</span>
                    <div className="flex flex-col min-w-0">
                        <span className="text-13 text-fg-1 truncate">{heading}</span>
                        {row.lineDescription ? (
                            <span className="text-12 text-fg-3 truncate">
                                {row.lineDescription}
                            </span>
                        ) : null}
                    </div>
                    {showAccountColumn && (
                        <span className="text-12 text-fg-2 truncate">{row.accountName}</span>
                    )}
                    {counterLabel}
                    {amount}
                </Link>
            </div>
            <div className="lg:hidden flex gap-3 px-2 py-3">
                <span className="flex items-start pt-[2px]">{checkbox}</span>
                <Link
                    to="/journal/$id"
                    params={{ id: row.journalEntryId }}
                    className="flex-1 flex flex-col gap-1 min-w-0"
                >
                    <div className="flex items-center justify-between gap-3">
                        <span className="text-12 text-fg-3 tabular">{row.date}</span>
                        {amount}
                    </div>
                    <span className="text-13 text-fg-1 truncate">{heading}</span>
                    {row.lineDescription ? (
                        <span className="text-12 text-fg-3 truncate">{row.lineDescription}</span>
                    ) : null}
                    {showAccountColumn && (
                        <span className="text-12 text-fg-2 truncate">{row.accountName}</span>
                    )}
                    {counterLabel}
                </Link>
            </div>
        </div>
    );
}

function DeleteAccountDialog({ account, onClose }: { account: Account; onClose: () => void }) {
    const del = useDeleteAccount();
    const toast = useToast();
    const navigate = useNavigate();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(account.id);
            toast.success(`Deleted “${account.name}”.`);
            await navigate({ to: '/accounts' });
        } catch (err) {
            handleActionError(err, { setError, toast: toast.error });
        }
    }

    return (
        <ConfirmDialog
            open
            onClose={onClose}
            onConfirm={() => void onConfirm()}
            title={`Delete “${account.name}”?`}
            message="This can't be undone. Accounts with journal entries can't be deleted until those are removed first."
            confirmLabel="Delete"
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
