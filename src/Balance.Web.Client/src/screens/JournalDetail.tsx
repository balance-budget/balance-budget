import { useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import {
    toUpdateInput,
    useDeleteJournalEntry,
    useJournalEntry,
    useUpdateJournalEntry,
    type JournalEntry,
    type JournalLine,
} from '../api/journalEntries';
import { useCounterparties } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import { asCounterpartyId, type JournalEntryId } from '../lib/domain';
import { ApiError } from '../lib/http';
import { formatMoney } from '../lib/money';

type DraftLine = { id: string; description: string };
type Draft = {
    date: string;
    description: string;
    counterpartyId: string;
    lines: DraftLine[];
};

function toDraft(entry: JournalEntry): Draft {
    return {
        date: entry.date,
        description: entry.description ?? '',
        counterpartyId: entry.counterpartyId ?? '',
        lines: entry.lines.map(line => ({ id: line.id, description: line.description ?? '' })),
    };
}

export function JournalDetail({ id }: { id: JournalEntryId }) {
    const query = useJournalEntry(id);
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
                <ErrorState
                    message="Couldn't load journal entry."
                    onRetry={() => void query.refetch()}
                />
            </Panel>
        );
    }

    const entry = query.data;

    if (editing) {
        return (
            <EditJournalEntry
                entry={entry}
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
                    onEdit={() => {
                        setEditing(true);
                    }}
                    onDelete={() => {
                        setDeleting(true);
                    }}
                />
            </Panel>

            <Panel>
                <SectionHead
                    title="Lines"
                    subtitle="Double-entry detail. Edit the entry to change line descriptions."
                />
                <LineTable entry={entry} />
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

function DetailHeader({
    entry,
    onEdit,
    onDelete,
}: {
    entry: JournalEntry;
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
                    {entry.bankTransactionId ? (
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
                <FromToSummary entry={entry} />
            </div>
            <div className="flex items-center gap-3 shrink-0">
                <HeaderAmount entry={entry} />
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

function FromToSummary({ entry }: { entry: JournalEntry }) {
    if (!entry.isSimplifiable) {
        return (
            <span className="text-[12px] text-fg-2 mt-1">
                Split ({entry.lineCount.toLocaleString('en-US')} lines)
            </span>
        );
    }

    const from = legLabel(entry.fromLegs);
    const to = legLabel(entry.toLegs);

    return (
        <span className="text-[12px] text-fg-2 mt-1 inline-flex items-center gap-1">
            <span>{from}</span>
            <Icon name="chevron-right" size={10} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span>{to}</span>
        </span>
    );
}

function legLabel(legs: JournalEntry['fromLegs']): string {
    const first = legs[0];
    if (!first) return '—';
    if (legs.length === 1) return first.accountName;
    return `${first.accountName} +${legs.length - 1}`;
}

function HeaderAmount({ entry }: { entry: JournalEntry }) {
    // ADR-0012: transfers render unsigned magnitude, muted; operating entries
    // render the signed net-worth change with colour by sign.
    const money = entry.isTransfer ? entry.grossMagnitude : entry.netWorthChange;
    const colour = entry.isTransfer
        ? 'text-fg-3'
        : money.amount < 0
          ? 'text-danger'
          : 'text-success';
    return (
        <Amount
            minor={money.amount}
            currencyCode={money.currencyCode}
            size="big"
            sign={!entry.isTransfer}
            className={colour}
        />
    );
}

function LineTable({ entry }: { entry: JournalEntry }) {
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
                    currencyCode={entry.netWorthChange.currencyCode}
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
    onCancel,
    onSaved,
}: {
    entry: JournalEntry;
    onCancel: () => void;
    onSaved: () => void;
}) {
    const update = useUpdateJournalEntry();
    const counterparties = useCounterparties();
    const catalog = useCurrencyCatalog();
    const toast = useToast();

    const [draft, setDraft] = useState<Draft>(() => toDraft(entry));
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const counterpartyList = counterparties.data ?? [];

    function patch(patch: Partial<Draft>) {
        setDraft(prev => ({ ...prev, ...patch }));
    }

    function patchLine(lineId: string, description: string) {
        setDraft(prev => ({
            ...prev,
            lines: prev.lines.map(l => (l.id === lineId ? { ...l, description } : l)),
        }));
    }

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        try {
            const original = toUpdateInput(entry);
            const trimmedDescription = draft.description.trim();
            const edited = {
                date: draft.date,
                description: trimmedDescription.length === 0 ? null : trimmedDescription,
                counterpartyId:
                    draft.counterpartyId === '' ? null : asCounterpartyId(draft.counterpartyId),
                lines: Object.fromEntries(
                    draft.lines.map(line => {
                        const trimmed = line.description.trim();
                        return [line.id, { description: trimmed.length === 0 ? null : trimmed }];
                    }),
                ),
            };
            await update.mutateAsync({ id: entry.id, original, edited });
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
            <Panel>
                <SectionHead
                    title="Edit entry"
                    subtitle="Account, debit, credit, and reconciliation status are fixed once an entry exists."
                />
                <FormErrorBanner message={topError} />
                <div className="grid grid-cols-[140px_1fr_minmax(180px,260px)] gap-3 mb-4">
                    <label className="flex flex-col gap-1">
                        <span className="text-[12px] font-medium text-fg-2">Date</span>
                        <input
                            type="date"
                            value={draft.date}
                            onChange={e => {
                                patch({ date: e.target.value });
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
                            value={draft.description}
                            onChange={e => {
                                patch({ description: e.target.value });
                            }}
                            maxLength={500}
                            placeholder="Optional"
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                        <FieldError name="Description" errors={fieldErrors} />
                    </label>
                    <label className="flex flex-col gap-1">
                        <span className="text-[12px] font-medium text-fg-2">Counterparty</span>
                        <select
                            value={draft.counterpartyId}
                            onChange={e => {
                                patch({ counterpartyId: e.target.value });
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        >
                            <option value="">None</option>
                            {counterpartyList.map(c => (
                                <option key={c.id} value={c.id}>
                                    {c.name}
                                </option>
                            ))}
                        </select>
                        <FieldError name="CounterpartyId" errors={fieldErrors} />
                    </label>
                </div>
            </Panel>

            <Panel>
                <SectionHead title="Lines" subtitle="Only line descriptions are editable." />
                <EditLineTable
                    entry={entry}
                    draft={draft}
                    catalog={catalog}
                    onLineDescription={patchLine}
                    fieldErrors={fieldErrors}
                />
                <div className="flex items-center justify-end gap-2 mt-4 pt-3 border-t border-border-soft">
                    <button
                        type="button"
                        onClick={onCancel}
                        disabled={update.isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={update.isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {update.isPending ? 'Saving…' : 'Save'}
                    </button>
                </div>
            </Panel>
        </form>
    );
}

function EditLineTable({
    entry,
    draft,
    catalog,
    onLineDescription,
    fieldErrors,
}: {
    entry: JournalEntry;
    draft: Draft;
    catalog: CurrencyCatalog;
    onLineDescription: (lineId: string, description: string) => void;
    fieldErrors: Record<string, string[]> | null;
}) {
    const currencyCode = entry.netWorthChange.currencyCode;
    const linesById = new Map<string, JournalLine>(entry.lines.map(l => [l.id, l]));

    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[1fr_120px_120px_140px_minmax(160px,1.4fr)] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Account</span>
                <span className="text-right">Debit</span>
                <span className="text-right">Credit</span>
                <span>Status</span>
                <span>Description</span>
            </div>
            {draft.lines.map(draftLine => {
                const line = linesById.get(draftLine.id);
                if (!line) return null;
                const isDebit = line.amount > 0;
                const magnitude = Math.abs(line.amount);
                return (
                    <div
                        key={draftLine.id}
                        className="grid grid-cols-[1fr_120px_120px_140px_minmax(160px,1.4fr)] gap-3 items-center px-2 py-2 border-b border-border-soft last:border-b-0"
                    >
                        <span className="text-[13px] text-fg-1 truncate">{line.accountName}</span>
                        <span className="font-mono text-[13px] tabular text-right text-fg-1">
                            {isDebit ? formatMoney(magnitude, currencyCode, catalog) : ''}
                        </span>
                        <span className="font-mono text-[13px] tabular text-right text-fg-1">
                            {!isDebit ? formatMoney(magnitude, currencyCode, catalog) : ''}
                        </span>
                        <ReconciliationChip status={line.reconciliationStatus} />
                        <div className="flex flex-col gap-1">
                            <input
                                type="text"
                                value={draftLine.description}
                                onChange={e => {
                                    onLineDescription(draftLine.id, e.target.value);
                                }}
                                maxLength={500}
                                placeholder="Optional"
                                className="px-2 py-1 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] focus:outline-none focus:border-border-strong"
                            />
                            <FieldError
                                name={`Lines[${draftLine.id}].Description`}
                                errors={fieldErrors}
                            />
                        </div>
                    </div>
                );
            })}
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
