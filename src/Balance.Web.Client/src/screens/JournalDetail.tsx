import { useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import {
    useDeleteJournalEntry,
    useJournalEntry,
    type JournalEntry,
    type JournalLine,
} from '../api/journalEntries';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import { type JournalEntryId } from '../lib/domain';
import { ApiError } from '../lib/http';
import { formatMoney } from '../lib/money';

export function JournalDetail({ id }: { id: JournalEntryId }) {
    const query = useJournalEntry(id);
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

    return (
        <>
            <Panel>
                <DetailHeader
                    entry={entry}
                    onDelete={() => {
                        setDeleting(true);
                    }}
                />
            </Panel>

            <Panel>
                <SectionHead
                    title="Lines"
                    subtitle="Double-entry detail. Editing isn't built yet."
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

function DetailHeader({ entry, onDelete }: { entry: JournalEntry; onDelete: () => void }) {
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
                        disabled
                        title="Editing journal entries isn't built yet."
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 opacity-40 cursor-not-allowed"
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
