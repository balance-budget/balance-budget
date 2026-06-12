import { useMemo, useState } from 'react';
import { Form } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
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
import { useCounterparties, useCreateCounterparty } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { AccountSelect } from '../components/AccountSelect';
import { BankTransactionDetails } from '../components/BankTransactionDetails';
import { ComboBox } from '../components/ui/ComboBox';
import { type ComboBoxItem } from '../components/ui/combobox.state';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { ProjectionAmount } from '../components/ProjectionAmount';
import { Skeleton } from '../components/Skeleton';
import { Button, IconButton } from '../components/ui/Button';
import { DatePicker } from '../components/ui/DatePicker';
import { NumberField } from '../components/ui/NumberField';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { formatNumber } from '../i18n/format';
import { accountPathLabel } from '../lib/accountTree';
import { cx } from '../lib/cx';
import { formatTableDate } from '../lib/dates';
import { type AccountId, type CounterpartyId, type JournalEntryId } from '../lib/domain';
import { handleActionError, handleFormError } from '../lib/formErrors';
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

export function JournalDetail({ id }: { id: JournalEntryId }) {
    const { t } = useLingui();
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
                    message={t`Couldn't load journal entry.`}
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
                <SectionHead
                    title={<Trans>Lines</Trans>}
                    subtitle={<Trans>Double-entry detail.</Trans>}
                />
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
    const { t } = useLingui();
    const detach = useDetachBankTransaction();
    const toast = useToast();

    async function onDetachClick() {
        try {
            await detach.mutateAsync(bt.id);
            toast.success(t`Detached. Bank transaction returned to the inbox.`);
        } catch (err) {
            if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <div className="flex items-start justify-between gap-3 mb-2">
            <SectionHead
                title={<Trans>Bank transaction</Trans>}
                subtitle={<Trans>Imported row this journal entry was categorized from.</Trans>}
            />
            <button
                type="button"
                onClick={() => void onDetachClick()}
                disabled={detach.isPending}
                aria-label={t`Detach bank transaction`}
                title={t`Detach this bank transaction - returns it to the inbox and clears the matching line back to Uncleared.`}
                className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-fg-2 border border-border-strong hover:bg-surface-2 disabled:opacity-60"
            >
                <Icon name="unlink" size={14} strokeWidth={2} />
                {detach.isPending ? <Trans>Detaching…</Trans> : <Trans>Detach</Trans>}
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
    const { t } = useLingui();
    return (
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div className="flex flex-col gap-[2px] min-w-0">
                <Link
                    to="/activity"
                    search={{ page: 1, q: '', account: '', from: '', to: '' }}
                    className="text-xs text-fg-3 hover:text-fg-1 inline-flex items-center gap-1"
                >
                    <Trans>← Activity</Trans>
                </Link>
                <div className="flex items-center gap-2 mt-1">
                    <h1 className="text-xl font-medium text-fg-1 truncate">
                        {entry.counterpartyName ?? entry.description ?? '—'}
                    </h1>
                    {entry.bankTransactions.length > 0 ? (
                        <span
                            title={t`From bank import`}
                            className="inline-flex items-center text-fg-3"
                        >
                            <Icon name="download" size={14} strokeWidth={2} />
                        </span>
                    ) : null}
                </div>
                <span className="text-xs text-fg-3 tabular-nums">
                    {formatTableDate(entry.date)}
                    {entry.counterpartyName && entry.description ? ` · ${entry.description}` : ''}
                </span>
                <FromToSummary projection={projection} lineCount={entry.lines.length} />
            </div>
            <div className="flex items-center justify-between gap-3 lg:shrink-0">
                <ProjectionAmount projection={projection} variant="header" />
                <div className="flex items-center gap-2">
                    <button
                        type="button"
                        onClick={onEdit}
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                    >
                        <Icon name="pencil" size={14} strokeWidth={2} />
                        <Trans>Edit</Trans>
                    </button>
                    <button
                        type="button"
                        onClick={onDelete}
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
                    >
                        <Icon name="trash" size={14} strokeWidth={2} />
                        <Trans>Delete</Trans>
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
            <span className="text-xs text-fg-2 mt-1">
                <Trans>Split ({formatNumber(lineCount)} lines)</Trans>
            </span>
        );
    }

    const from = formatLegLabel(projection.fromLegs);
    const to = formatLegLabel(projection.toLegs);

    return (
        <span className="text-xs text-fg-2 mt-1 inline-flex items-center gap-1">
            <span>{from}</span>
            <Icon name="chevron-right" size={10} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span>{to}</span>
        </span>
    );
}

function LineTable({ entry, projection }: { entry: JournalEntry; projection: JournalProjection }) {
    const catalog = useCurrencyCatalog();
    return (
        <div className="flex flex-col">
            <div className="hidden lg:grid grid-cols-[1fr_120px_120px_140px_minmax(120px,1.4fr)] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>
                    <Trans>Account</Trans>
                </span>
                <span className="text-right">
                    <Trans>Debit</Trans>
                </span>
                <span className="text-right">
                    <Trans>Credit</Trans>
                </span>
                <span>
                    <Trans>Status</Trans>
                </span>
                <span>
                    <Trans>Description</Trans>
                </span>
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
    const moneyStr = formatMoney(magnitude, currencyCode, catalog);
    return (
        <div className="border-b border-border-soft last:border-b-0">
            <div className="hidden lg:grid grid-cols-[1fr_120px_120px_140px_minmax(120px,1.4fr)] gap-3 items-center px-2 py-2">
                <span className="text-sm text-fg-1 truncate">{line.accountName}</span>
                <span className="font-mono text-sm tabular-nums text-right text-fg-1">
                    {isDebit ? moneyStr : ''}
                </span>
                <span className="font-mono text-sm tabular-nums text-right text-fg-1">
                    {!isDebit ? moneyStr : ''}
                </span>
                <ReconciliationChip status={line.reconciliationStatus} />
                <span className="text-xs text-fg-3 truncate">{line.description ?? ''}</span>
            </div>
            <div className="lg:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-baseline justify-between gap-3">
                    <span className="text-sm text-fg-1 truncate">{line.accountName}</span>
                    <span className="font-mono text-sm tabular-nums shrink-0 text-fg-1">
                        {isDebit ? <Trans>Dr </Trans> : <Trans>Cr </Trans>}
                        {moneyStr}
                    </span>
                </div>
                <div className="flex items-center justify-between gap-2">
                    <ReconciliationChip status={line.reconciliationStatus} />
                    {line.description ? (
                        <span className="text-xs text-fg-3 truncate">{line.description}</span>
                    ) : null}
                </div>
            </div>
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
                'inline-flex items-center justify-center px-2 py-[2px] rounded-lg text-xs font-medium w-fit',
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
    const { t } = useLingui();
    const replace = useReplaceJournalEntry();
    const counterparties = useCounterparties();
    const createCounterparty = useCreateCounterparty();
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

    const counterpartyItems = useMemo<ComboBoxItem<CounterpartyId | null>[]>(
        () =>
            [...(counterparties.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name))
                .map(c => ({ key: c.id, label: c.name, value: c.id })),
        [counterparties.data],
    );

    async function createAndSelectCounterparty(name: string) {
        try {
            const created = await createCounterparty.mutateAsync({ name });
            setCounterpartyId(created.id);
            toast.success(t`Counterparty '${created.name}' created.`);
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

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
            toast.success(t`Journal entry saved.`);
            onSaved();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Form
            validationErrors={fieldErrors ?? undefined}
            onSubmit={e => {
                e.preventDefault();
                void submit();
            }}
        >
            {entry.bankTransactions.map(bt => (
                <Panel key={bt.id}>
                    <SectionHead
                        title={<Trans>Bank transaction</Trans>}
                        subtitle={
                            <Trans>Imported row this journal entry was categorized from.</Trans>
                        }
                    />
                    <BankTransactionDetails bt={bt} catalog={catalog} />
                </Panel>
            ))}

            <Panel>
                <SectionHead
                    title={<Trans>Edit journal entry</Trans>}
                    subtitle={
                        <Trans>
                            Cleared and Reconciled lines are frozen - only their description can be
                            edited.
                        </Trans>
                    }
                />
                <FormErrorBanner message={topError} />
                <div className="grid grid-cols-1 lg:grid-cols-[140px_1fr_minmax(180px,260px)] gap-3 mb-4">
                    <DatePicker
                        label={t`Date`}
                        name="Date"
                        value={date}
                        onChange={setDate}
                        isRequired
                    />
                    <TextField
                        label={t`Description`}
                        name="Description"
                        value={description}
                        onChange={setDescription}
                        maxLength={500}
                        placeholder={t`Optional`}
                    />
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            <Trans>Counterparty</Trans>
                        </span>
                        <ComboBox
                            name="CounterpartyId"
                            items={counterpartyItems}
                            value={counterpartyId}
                            onChange={id => {
                                setCounterpartyId(id);
                            }}
                            onClear={() => {
                                setCounterpartyId(null);
                            }}
                            onCreate={name => {
                                void createAndSelectCounterparty(name);
                            }}
                            noneLabel={t`── None`}
                            placeholder={t`Pick counterparty…`}
                            ariaLabel={t`Counterparty`}
                        />
                    </div>
                </div>
            </Panel>

            <Panel>
                <EditLines
                    lines={lines}
                    currencyCode={currencyCode}
                    accountsById={accountsById}
                    fieldErrors={fieldErrors}
                    onUpdate={updateLine}
                    onAdd={addLine}
                    onRemove={removeLine}
                />
                <BalanceFooter totals={totals} currencyCode={currencyCode} catalog={catalog} />
                <div className="flex items-center justify-end gap-2 mt-4 pt-3 border-t border-border-soft">
                    <Button variant="ghost" onPress={onCancel} isDisabled={replace.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button
                        type="submit"
                        variant="primary"
                        isDisabled={replace.isPending || !totals.balanced}
                    >
                        {replace.isPending ? <Trans>Saving…</Trans> : <Trans>Save</Trans>}
                    </Button>
                </div>
            </Panel>
        </Form>
    );
}

function EditLines({
    lines,
    currencyCode,
    accountsById,
    fieldErrors,
    onUpdate,
    onAdd,
    onRemove,
}: {
    lines: EditLine[];
    currencyCode: string;
    accountsById: ReadonlyMap<AccountId, Account>;
    fieldErrors: FieldErrors | null;
    onUpdate: (key: string, patch: Partial<EditLine>) => void;
    onAdd: () => void;
    onRemove: (key: string) => void;
}) {
    return (
        <div className="flex flex-col">
            <div className="hidden lg:grid grid-cols-[1fr_90px_140px_140px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>
                    <Trans>Account</Trans>
                </span>
                <span>
                    <Trans>Side</Trans>
                </span>
                <span className="text-right">
                    <Trans>Amount</Trans>
                </span>
                <span>
                    <Trans>Status</Trans>
                </span>
                <span>
                    <Trans>Description</Trans>
                </span>
                <span />
            </div>
            {lines.map((line, i) => (
                <EditLineRow
                    key={line.key}
                    line={line}
                    index={i}
                    currencyCode={currencyCode}
                    accountsById={accountsById}
                    onUpdate={onUpdate}
                    onRemove={onRemove}
                />
            ))}
            <FieldError name="lines" errors={fieldErrors} />
            <div className="mt-2">
                <button
                    type="button"
                    onClick={onAdd}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                >
                    <Icon name="plus" size={12} strokeWidth={2} />
                    <Trans>Add line</Trans>
                </button>
            </div>
        </div>
    );
}

function EditLineRow({
    line,
    index,
    currencyCode,
    accountsById,
    onUpdate,
    onRemove,
}: {
    line: EditLine;
    index: number;
    currencyCode: string;
    accountsById: ReadonlyMap<AccountId, Account>;
    onUpdate: (key: string, patch: Partial<EditLine>) => void;
    onRemove: (key: string) => void;
}) {
    const { t } = useLingui();
    const locked = isLineLocked(line);
    // A frozen line shows the same code + path the picker would, so editable and
    // frozen rows read identically.
    const frozenLabel =
        line.accountId !== null ? (accountPathLabel(accountsById, line.accountId) ?? '—') : '—';
    return (
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_90px_140px_140px_minmax(140px,1fr)_32px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0">
            <div className="flex flex-col gap-1">
                {locked ? (
                    <span
                        className="px-3 py-2 rounded-lg bg-surface-1 border border-border-soft text-fg-2 text-sm truncate"
                        title={t`Frozen - line is Cleared or Reconciled`}
                    >
                        {frozenLabel}
                    </span>
                ) : (
                    <AccountSelect
                        name={`lines[${index.toString()}].accountId`}
                        value={line.accountId}
                        onChange={id => {
                            onUpdate(line.key, { accountId: id });
                        }}
                        postableOnly
                        currencyCode={currencyCode}
                        placeholder={t`Pick account…`}
                        ariaLabel={t`Account`}
                    />
                )}
            </div>
            <Select
                aria-label={t`Side`}
                value={line.side}
                isDisabled={locked}
                onChange={key => {
                    if (key !== null) onUpdate(line.key, { side: key as EditLine['side'] });
                }}
            >
                <SelectItem id="debit">
                    <Trans>Debit</Trans>
                </SelectItem>
                <SelectItem id="credit">
                    <Trans>Credit</Trans>
                </SelectItem>
            </Select>
            <NumberField
                aria-label={t`Amount`}
                name={`lines[${index.toString()}].amount`}
                value={line.amount === '' ? NaN : Number(line.amount)}
                isDisabled={locked}
                onChange={n => {
                    onUpdate(line.key, { amount: Number.isNaN(n) ? '' : String(n) });
                }}
                formatOptions={{ style: 'currency', currency: currencyCode }}
                placeholder="0.00"
                inputClassName="text-right font-mono"
            />
            <ReconciliationChip status={line.status} />
            <TextField
                aria-label={t`Line description`}
                value={line.description}
                onChange={description => {
                    onUpdate(line.key, { description });
                }}
                maxLength={500}
                placeholder={t`Optional`}
                inputClassName="text-sm"
            />
            <IconButton
                onPress={() => {
                    onRemove(line.key);
                }}
                isDisabled={locked}
                aria-label={locked ? t`Frozen - line cannot be removed` : t`Remove this line`}
                className="self-end lg:self-start mt-[6px] data-[hovered]:text-danger data-[hovered]:bg-transparent"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </IconButton>
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
        <div className="flex items-center justify-end gap-4 mt-3 text-xs tabular-nums">
            <span className="text-fg-3">
                <Trans>
                    Σ Debit <span className="font-mono text-fg-1">{debitStr}</span>
                </Trans>
            </span>
            <span className="text-fg-3">
                <Trans>
                    Σ Credit <span className="font-mono text-fg-1">{creditStr}</span>
                </Trans>
            </span>
            {totals.balanced ? (
                <span className="inline-flex items-center gap-1 text-success">
                    <Icon name="check-circle" size={12} strokeWidth={2} /> <Trans>Balanced</Trans>
                </span>
            ) : (
                <span className="inline-flex items-center gap-1 text-danger">
                    <Icon name="alert-circle" size={12} strokeWidth={2} />
                    <Trans>Off by {diffStr}</Trans>
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
    const { t } = useLingui();
    const del = useDeleteJournalEntry();
    const toast = useToast();
    const navigate = useNavigate();
    const [error, setError] = useState<string | null>(null);

    const label = entry.counterpartyName ?? entry.description ?? entry.date;

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(entry.id);
            toast.success(t`Deleted journal entry “${label}”.`);
            await navigate({
                to: '/activity',
                search: { page: 1, q: '', account: '', from: '', to: '' },
            });
        } catch (err) {
            handleActionError(err, { setError, toast: toast.error });
        }
    }

    return (
        <ConfirmDialog
            open
            onClose={onClose}
            onConfirm={() => void onConfirm()}
            title={t`Delete this journal entry?`}
            message={t`This can't be undone. The bookkeeping lines on every affected account will disappear with it.`}
            confirmLabel={t`Delete`}
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
