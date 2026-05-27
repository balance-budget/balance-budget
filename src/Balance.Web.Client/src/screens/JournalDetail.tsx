import { useMemo, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import {
    useDeleteJournalEntry,
    useJournalEntry,
    useReplaceJournalEntry,
    type JournalEntry,
    type JournalEntryDetail,
    type JournalLine,
} from '../api/journalEntries';
import { useDetachBankTransaction, type BankTransactionDetail } from '../api/bankTransactions';
import { useCounterparties } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { Amount } from '../components/Amount';
import { BankTransactionDetails } from '../components/BankTransactionDetails';
import { Combobox } from '../components/Combobox';
import { type ComboboxItem } from '../components/combobox.state';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import {
    type AccountId,
    type AccountType,
    type CounterpartyId,
    type JournalEntryId,
} from '../lib/domain';
import { ApiError } from '../lib/http';
import { formatLegLabel, projectEntry, type JournalProjection } from '../lib/journalProjection';
import { formatMoney } from '../lib/money';
import {
    buildReplaceRequest,
    computeTotals,
    emptyLine,
    isLineLocked,
    toEditLines,
    type EditLine,
    type FieldErrors,
    type TotalsState,
} from './journalDetail.state';

const ACCOUNT_TYPE_ORDER: AccountType[] = ['Asset', 'Liability', 'Income', 'Expense', 'Equity'];

const ACCOUNT_TYPE_LABEL: Record<AccountType, string> = {
    Asset: 'Assets',
    Liability: 'Liabilities',
    Income: 'Income',
    Expense: 'Expenses',
    Equity: 'Equity',
};

export function JournalDetail({ id }: { id: JournalEntryId }) {
    const query = useJournalEntry(id);
    const accounts = useAccounts();
    const catalog = useCurrencyCatalog();
    const [editing, setEditing] = useState(false);
    const [deleting, setDeleting] = useState(false);

    const accountById = useMemo(
        () => new Map<AccountId, Account>((accounts.data ?? []).map(a => [a.id, a])),
        [accounts.data],
    );

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
                <ErrorState
                    message="Couldn't load journal entry."
                    onRetry={() => void query.refetch()}
                />
            </Panel>
        );
    }

    const entry = query.data;
    const projection = projectEntry(entry, accountById);

    if (editing) {
        return (
            <EditJournalEntry
                entry={entry}
                accounts={accounts.data ?? []}
                onCancel={() => {
                    setEditing(false);
                }}
                onSaved={() => {
                    setEditing(false);
                }}
            />
        );
    }

    return (
        <>
            <Panel>
                <DetailHeader
                    entry={entry}
                    projection={projection}
                    onEdit={() => {
                        setEditing(true);
                    }}
                    onDelete={() => {
                        setDeleting(true);
                    }}
                />
            </Panel>

            {entry.bankTransactions.map(bt => (
                <Panel key={bt.id}>
                    <BankTransactionPanelHead bt={bt} />
                    <BankTransactionDetails bt={bt} catalog={catalog} />
                </Panel>
            ))}

            <Panel>
                <SectionHead title="Lines" subtitle="Double-entry detail." />
                <LineTable entry={entry} projection={projection} />
            </Panel>

            {deleting && (
                <DeleteJournalEntryDialog
                    entry={entry}
                    onClose={() => {
                        setDeleting(false);
                    }}
                />
            )}
        </>
    );
}

function BankTransactionPanelHead({ bt }: { bt: BankTransactionDetail }) {
    const detach = useDetachBankTransaction();
    const toast = useToast();

    async function onDetachClick() {
        try {
            await detach.mutateAsync(bt.id);
            toast.success('Detached. Bank transaction returned to the inbox.');
        } catch (err) {
            if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <div className="flex items-start justify-between gap-3 mb-2">
            <SectionHead
                title="Bank transaction"
                subtitle="Imported row this entry was categorised from."
            />
            <button
                type="button"
                onClick={() => void onDetachClick()}
                disabled={detach.isPending}
                aria-label="Detach bank transaction"
                title="Detach this BT — returns it to the inbox and clears the matching line back to Uncleared."
                className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-fg-2 border border-border-strong hover:bg-surface-2 disabled:opacity-60"
            >
                <Icon name="unlink" size={14} strokeWidth={2} />
                {detach.isPending ? 'Detaching…' : 'Detach'}
            </button>
        </div>
    );
}

function DetailHeader({
    entry,
    projection,
    onEdit,
    onDelete,
}: {
    entry: JournalEntryDetail;
    projection: JournalProjection;
    onEdit: () => void;
    onDelete: () => void;
}) {
    return (
        <div className="flex items-start justify-between gap-3">
            <div className="flex flex-col gap-[2px] min-w-0">
                <Link
                    to="/journal"
                    search={{ page: 1 }}
                    className="text-[12px] text-fg-3 hover:text-fg-1 inline-flex items-center gap-1"
                >
                    ← Journal entries
                </Link>
                <div className="flex items-center gap-2 mt-1">
                    <h1 className="text-[22px] font-medium text-fg-1 truncate">
                        {entry.counterpartyName ?? entry.description ?? '—'}
                    </h1>
                    {entry.bankTransactions.length > 0 ? (
                        <span
                            title="From bank import"
                            className="inline-flex items-center text-fg-3"
                        >
                            <Icon name="download" size={14} strokeWidth={2} />
                        </span>
                    ) : null}
                </div>
                <span className="text-[12px] text-fg-3 tabular">
                    {entry.date}
                    {entry.counterpartyName && entry.description ? ` · ${entry.description}` : ''}
                </span>
                <FromToSummary projection={projection} lineCount={entry.lines.length} />
            </div>
            <div className="flex items-center gap-3 shrink-0">
                <HeaderAmount projection={projection} />
                <div className="flex items-center gap-2">
                    <button
                        type="button"
                        onClick={onEdit}
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                    >
                        <Icon name="pencil" size={14} strokeWidth={2} />
                        Edit
                    </button>
                    <button
                        type="button"
                        onClick={onDelete}
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
                    >
                        <Icon name="trash" size={14} strokeWidth={2} />
                        Delete
                    </button>
                </div>
            </div>
        </div>
    );
}

function FromToSummary({
    projection,
    lineCount,
}: {
    projection: JournalProjection;
    lineCount: number;
}) {
    if (!projection.isSimplifiable) {
        return (
            <span className="text-[12px] text-fg-2 mt-1">
                Split ({lineCount.toLocaleString('en-US')} lines)
            </span>
        );
    }

    const from = formatLegLabel(projection.fromLegs);
    const to = formatLegLabel(projection.toLegs);

    return (
        <span className="text-[12px] text-fg-2 mt-1 inline-flex items-center gap-1">
            <span>{from}</span>
            <Icon name="chevron-right" size={10} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span>{to}</span>
        </span>
    );
}

function HeaderAmount({ projection }: { projection: JournalProjection }) {
    // ADR-0012: transfers render unsigned magnitude, muted; operating entries
    // render the signed net-worth change with colour by sign.
    const money = projection.isTransfer ? projection.grossMagnitude : projection.netWorthChange;
    const colour = projection.isTransfer
        ? 'text-fg-3'
        : money.amount < 0
          ? 'text-danger'
          : 'text-success';
    return (
        <Amount
            minor={money.amount}
            currencyCode={money.currencyCode}
            size="big"
            sign={!projection.isTransfer}
            className={colour}
        />
    );
}

function LineTable({ entry, projection }: { entry: JournalEntry; projection: JournalProjection }) {
    const catalog = useCurrencyCatalog();
    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[1fr_120px_120px_140px_minmax(120px,1.4fr)] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Account</span>
                <span className="text-right">Debit</span>
                <span className="text-right">Credit</span>
                <span>Status</span>
                <span>Description</span>
            </div>
            {entry.lines.map(line => (
                <LineRow
                    key={line.id}
                    line={line}
                    currencyCode={projection.netWorthChange.currencyCode}
                    catalog={catalog}
                />
            ))}
        </div>
    );
}

function LineRow({
    line,
    currencyCode,
    catalog,
}: {
    line: JournalLine;
    currencyCode: string;
    catalog: CurrencyCatalog;
}) {
    const isDebit = line.amount > 0;
    const magnitude = Math.abs(line.amount);
    return (
        <div className="grid grid-cols-[1fr_120px_120px_140px_minmax(120px,1.4fr)] gap-3 items-center px-2 py-2 border-b border-border-soft last:border-b-0">
            <span className="text-[13px] text-fg-1 truncate">{line.accountName}</span>
            <span className="font-mono text-[13px] tabular text-right text-fg-1">
                {isDebit ? formatMoney(magnitude, currencyCode, catalog) : ''}
            </span>
            <span className="font-mono text-[13px] tabular text-right text-fg-1">
                {!isDebit ? formatMoney(magnitude, currencyCode, catalog) : ''}
            </span>
            <ReconciliationChip status={line.reconciliationStatus} />
            <span className="text-[12px] text-fg-3 truncate">{line.description ?? ''}</span>
        </div>
    );
}

const CHIP_CLASS: Record<JournalLine['reconciliationStatus'], string> = {
    Uncleared: 'bg-surface-2 text-fg-3',
    Cleared: 'bg-surface-2 text-fg-2',
    Reconciled: 'bg-success/10 text-success',
};

function ReconciliationChip({ status }: { status: JournalLine['reconciliationStatus'] }) {
    return (
        <span
            className={cx(
                'inline-flex items-center justify-center px-2 py-[2px] rounded-sm text-[11px] font-medium w-fit',
                CHIP_CLASS[status],
            )}
        >
            {status}
        </span>
    );
}

function EditJournalEntry({
    entry,
    accounts,
    onCancel,
    onSaved,
}: {
    entry: JournalEntryDetail;
    accounts: Account[];
    onCancel: () => void;
    onSaved: () => void;
}) {
    const replace = useReplaceJournalEntry();
    const counterparties = useCounterparties();
    const catalog = useCurrencyCatalog();
    const toast = useToast();

    const accountsById = useMemo(
        () => new Map<AccountId, Account>(accounts.map(a => [a.id, a])),
        [accounts],
    );

    const currencyCode = useMemo(() => {
        for (const line of entry.lines) {
            const code = accountsById.get(line.accountId)?.currencyCode;
            if (code) return code;
        }
        return 'EUR';
    }, [entry.lines, accountsById]);

    const scale = useMemo(() => {
        const currency = catalog.get(currencyCode);
        return currency?.minorUnitScale ?? 2;
    }, [catalog, currencyCode]);

    const [date, setDate] = useState(entry.date);
    const [description, setDescription] = useState(entry.description ?? '');
    const [counterpartyId, setCounterpartyId] = useState<CounterpartyId | null>(
        entry.counterpartyId,
    );
    const [lines, setLines] = useState<EditLine[]>(() => toEditLines(entry.lines, scale));
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<FieldErrors | null>(null);

    const counterpartyItems = useMemo<ComboboxItem<CounterpartyId | null>[]>(
        () =>
            [...(counterparties.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name))
                .map(c => ({ key: c.id, label: c.name, value: c.id })),
        [counterparties.data],
    );

    const visibleAccounts = useMemo(
        () => accounts.filter(a => a.currencyCode === currencyCode),
        [accounts, currencyCode],
    );

    function updateLine(key: string, patch: Partial<EditLine>) {
        setLines(prev => prev.map(l => (l.key === key ? { ...l, ...patch } : l)));
    }

    function addLine() {
        setLines(prev => [...prev, emptyLine()]);
    }

    function removeLine(key: string) {
        setLines(prev => prev.filter(l => l.key !== key));
    }

    const totals = computeTotals(lines, scale);

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        const result = buildReplaceRequest({
            date,
            description,
            counterpartyId,
            lines,
            scale,
        });
        if (!result.ok) {
            setFieldErrors(result.fieldErrors);
            if (result.topError) setTopError(result.topError);
            return;
        }
        try {
            await replace.mutateAsync({ id: entry.id, request: result.request });
            toast.success('Journal entry saved.');
            onSaved();
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
        <form
            onSubmit={e => {
                e.preventDefault();
                void submit();
            }}
            noValidate
        >
            {entry.bankTransactions.map(bt => (
                <Panel key={bt.id}>
                    <SectionHead
                        title="Bank transaction"
                        subtitle="Imported row this entry was categorised from."
                    />
                    <BankTransactionDetails bt={bt} catalog={catalog} />
                </Panel>
            ))}

            <Panel>
                <SectionHead
                    title="Edit entry"
                    subtitle="Cleared and Reconciled lines are frozen — only their description can be edited."
                />
                <FormErrorBanner message={topError} />
                <div className="grid grid-cols-[140px_1fr_minmax(180px,260px)] gap-3 mb-4">
                    <label className="flex flex-col gap-1">
                        <span className="text-[12px] font-medium text-fg-2">Date</span>
                        <input
                            type="date"
                            value={date}
                            onChange={e => {
                                setDate(e.target.value);
                            }}
                            required
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                        <FieldError name="Date" errors={fieldErrors} />
                    </label>
                    <label className="flex flex-col gap-1">
                        <span className="text-[12px] font-medium text-fg-2">Description</span>
                        <input
                            type="text"
                            value={description}
                            onChange={e => {
                                setDescription(e.target.value);
                            }}
                            maxLength={500}
                            placeholder="Optional"
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                        <FieldError name="Description" errors={fieldErrors} />
                    </label>
                    <div className="flex flex-col gap-1">
                        <span className="text-[12px] font-medium text-fg-2">Counterparty</span>
                        <Combobox
                            items={counterpartyItems}
                            value={counterpartyId}
                            onChange={id => {
                                setCounterpartyId(id);
                            }}
                            onClear={() => {
                                setCounterpartyId(null);
                            }}
                            noneLabel="── None"
                            placeholder="Pick counterparty…"
                            ariaLabel="Counterparty"
                        />
                        <FieldError name="CounterpartyId" errors={fieldErrors} />
                    </div>
                </div>
            </Panel>

            <Panel>
                <EditLines
                    lines={lines}
                    accounts={visibleAccounts}
                    fieldErrors={fieldErrors}
                    onUpdate={updateLine}
                    onAdd={addLine}
                    onRemove={removeLine}
                />
                <BalanceFooter totals={totals} currencyCode={currencyCode} catalog={catalog} />
                <div className="flex items-center justify-end gap-2 mt-4 pt-3 border-t border-border-soft">
                    <button
                        type="button"
                        onClick={onCancel}
                        disabled={replace.isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={replace.isPending || !totals.balanced}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {replace.isPending ? 'Saving…' : 'Save'}
                    </button>
                </div>
            </Panel>
        </form>
    );
}

function EditLines({
    lines,
    accounts,
    fieldErrors,
    onUpdate,
    onAdd,
    onRemove,
}: {
    lines: EditLine[];
    accounts: Account[];
    fieldErrors: FieldErrors | null;
    onUpdate: (key: string, patch: Partial<EditLine>) => void;
    onAdd: () => void;
    onRemove: (key: string) => void;
}) {
    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[1fr_90px_140px_140px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Account</span>
                <span>Side</span>
                <span className="text-right">Amount</span>
                <span>Status</span>
                <span>Description</span>
                <span />
            </div>
            {lines.map((line, i) => (
                <EditLineRow
                    key={line.key}
                    line={line}
                    index={i}
                    accounts={accounts}
                    fieldErrors={fieldErrors}
                    onUpdate={onUpdate}
                    onRemove={onRemove}
                />
            ))}
            <FieldError name="lines" errors={fieldErrors} />
            <div className="mt-2">
                <button
                    type="button"
                    onClick={onAdd}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                >
                    <Icon name="plus" size={12} strokeWidth={2} />
                    Add line
                </button>
            </div>
        </div>
    );
}

function EditLineRow({
    line,
    index,
    accounts,
    fieldErrors,
    onUpdate,
    onRemove,
}: {
    line: EditLine;
    index: number;
    accounts: Account[];
    fieldErrors: FieldErrors | null;
    onUpdate: (key: string, patch: Partial<EditLine>) => void;
    onRemove: (key: string) => void;
}) {
    const locked = isLineLocked(line);
    const accountItems = useMemo<ComboboxItem<AccountId>[]>(
        () =>
            [...accounts]
                .sort((a, b) => a.name.localeCompare(b.name))
                .map(a => ({ key: a.id, label: a.name, group: a.type, value: a.id })),
        [accounts],
    );
    const selectedAccount = useMemo(
        () => accounts.find(a => a.id === line.accountId),
        [accounts, line.accountId],
    );
    return (
        <div className="grid grid-cols-[1fr_90px_140px_140px_minmax(140px,1fr)_32px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0">
            <div className="flex flex-col gap-1">
                {locked ? (
                    <span
                        className="px-3 py-2 rounded-sm bg-surface-1 border border-border-soft text-fg-2 text-[13px] truncate"
                        title="Frozen — line is Cleared or Reconciled"
                    >
                        {selectedAccount?.name ?? '—'}
                    </span>
                ) : (
                    <Combobox
                        items={accountItems}
                        value={line.accountId}
                        onChange={id => {
                            onUpdate(line.key, { accountId: id });
                        }}
                        groupOrder={ACCOUNT_TYPE_ORDER}
                        groupLabels={ACCOUNT_TYPE_LABEL}
                        placeholder="Pick account…"
                        ariaLabel="Account"
                    />
                )}
                <FieldError name={`lines[${index.toString()}].accountId`} errors={fieldErrors} />
            </div>
            <select
                value={line.side}
                disabled={locked}
                onChange={e => {
                    onUpdate(line.key, { side: e.target.value as EditLine['side'] });
                }}
                className="px-2 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] focus:outline-none focus:border-border-strong disabled:opacity-60 disabled:cursor-not-allowed"
            >
                <option value="debit">Debit</option>
                <option value="credit">Credit</option>
            </select>
            <div className="flex flex-col gap-1">
                <input
                    type="text"
                    inputMode="decimal"
                    value={line.amount}
                    disabled={locked}
                    onChange={e => {
                        onUpdate(line.key, { amount: e.target.value });
                    }}
                    placeholder="0.00"
                    className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] text-right font-mono tabular focus:outline-none focus:border-border-strong disabled:opacity-60 disabled:cursor-not-allowed"
                />
                <FieldError name={`lines[${index.toString()}].amount`} errors={fieldErrors} />
            </div>
            <ReconciliationChip status={line.status} />
            <input
                type="text"
                value={line.description}
                onChange={e => {
                    onUpdate(line.key, { description: e.target.value });
                }}
                maxLength={500}
                placeholder="Optional"
                className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] focus:outline-none focus:border-border-strong"
            />
            <button
                type="button"
                onClick={() => {
                    onRemove(line.key);
                }}
                disabled={locked}
                title={locked ? 'Frozen — line cannot be removed' : 'Remove this line'}
                className="self-start mt-[6px] p-1 text-fg-3 hover:text-danger disabled:opacity-40 disabled:cursor-not-allowed"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function BalanceFooter({
    totals,
    currencyCode,
    catalog,
}: {
    totals: TotalsState;
    currencyCode: string;
    catalog: CurrencyCatalog;
}) {
    const debitStr = formatMoney(totals.debitMinor, currencyCode, catalog);
    const creditStr = formatMoney(totals.creditMinor, currencyCode, catalog);
    const diff = Math.abs(totals.debitMinor - totals.creditMinor);
    const diffStr = formatMoney(diff, currencyCode, catalog);
    return (
        <div className="flex items-center justify-end gap-4 mt-3 text-[12px] tabular">
            <span className="text-fg-3">
                Σ Debit <span className="font-mono text-fg-1">{debitStr}</span>
            </span>
            <span className="text-fg-3">
                Σ Credit <span className="font-mono text-fg-1">{creditStr}</span>
            </span>
            {totals.balanced ? (
                <span className="inline-flex items-center gap-1 text-success">
                    <Icon name="check-circle" size={12} strokeWidth={2} /> Balanced
                </span>
            ) : (
                <span className="inline-flex items-center gap-1 text-danger">
                    <Icon name="alert-circle" size={12} strokeWidth={2} />
                    Off by {diffStr}
                </span>
            )}
        </div>
    );
}

function DeleteJournalEntryDialog({
    entry,
    onClose,
}: {
    entry: JournalEntry;
    onClose: () => void;
}) {
    const del = useDeleteJournalEntry();
    const toast = useToast();
    const navigate = useNavigate();
    const [error, setError] = useState<string | null>(null);

    const label = entry.counterpartyName ?? entry.description ?? entry.date;

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(entry.id);
            toast.success(`Deleted journal entry “${label}”.`);
            await navigate({ to: '/journal', search: { page: 1 } });
        } catch (err) {
            if (err instanceof ApiError && err.status >= 400 && err.status < 500) {
                setError(err.message);
            } else if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <ConfirmDialog
            open
            onClose={onClose}
            onConfirm={() => void onConfirm()}
            title="Delete this journal entry?"
            message="This can't be undone. The bookkeeping lines on every affected account will disappear with it."
            confirmLabel="Delete"
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
