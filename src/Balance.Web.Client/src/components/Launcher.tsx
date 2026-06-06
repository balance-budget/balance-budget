import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import {
    useSearch,
    type AccountHit,
    type BankAccountHit,
    type CounterpartyHit,
    type JournalEntryHit,
    type PageHit,
} from '../api/search';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import { Icon } from './Icon';
import { cx } from '../lib/cx';

type LauncherProps = {
    open: boolean;
    onClose: () => void;
};

type FlatRow =
    | { kind: 'account'; hit: AccountHit }
    | { kind: 'counterparty'; hit: CounterpartyHit }
    | { kind: 'bankAccount'; hit: BankAccountHit }
    | { kind: 'journalEntry'; hit: JournalEntryHit }
    | { kind: 'page'; hit: PageHit };

/**
 * Cmd-K launcher: typeable input at the top, grouped results below, keyboard
 * nav (up/down/enter/esc), no recent-items or command surface. The dropdown
 * empty state shows a placeholder hint until the user types 2+ characters.
 */
export function Launcher({ open, onClose }: LauncherProps) {
    const dialogRef = useRef<HTMLDialogElement>(null);
    const inputRef = useRef<HTMLInputElement>(null);
    const [query, setQuery] = useState('');
    const debounced = useDebouncedValue(query, 200);
    const navigate = useNavigate();
    const search = useSearch(debounced);
    const [activeIndex, setActiveIndex] = useState(0);

    useEffect(() => {
        const dialog = dialogRef.current;
        if (!dialog) return;
        if (open && !dialog.open) {
            dialog.showModal();
            // Reset every time the launcher opens — never carry forward state from
            // a previous session.
            setQuery('');
            setActiveIndex(0);
            // Give the dialog a tick to settle so focus lands correctly.
            window.requestAnimationFrame(() => {
                inputRef.current?.focus();
            });
        } else if (!open && dialog.open) {
            dialog.close();
        }
    }, [open]);

    const rows = useMemo<FlatRow[]>(() => {
        if (!search.data) return [];
        const flat: FlatRow[] = [];
        for (const hit of search.data.accounts.items) flat.push({ kind: 'account', hit });
        for (const hit of search.data.counterparties.items)
            flat.push({ kind: 'counterparty', hit });
        for (const hit of search.data.bankAccounts.items) flat.push({ kind: 'bankAccount', hit });
        for (const hit of search.data.journalEntries.items)
            flat.push({ kind: 'journalEntry', hit });
        for (const hit of search.data.pages.items) flat.push({ kind: 'page', hit });
        return flat;
    }, [search.data]);

    // Clamp the active index in the render path so a shrinking result set
    // (user keeps typing) never points past the end.
    const effectiveIndex = rows.length === 0 ? 0 : Math.min(activeIndex, rows.length - 1);

    function navigateTo(row: FlatRow) {
        onClose();
        switch (row.kind) {
            case 'account':
                void navigate({
                    to: '/accounts/$id',
                    params: { id: row.hit.id },
                    search: {
                        page: 1,
                        q: '',
                        posted: '',
                        counter: '',
                        from: '',
                        to: '',
                        status: '',
                    },
                });
                break;
            case 'counterparty':
                void navigate({
                    to: '/counterparties/$id',
                    params: { id: row.hit.id },
                    search: { page: 1 },
                });
                break;
            case 'bankAccount':
                void navigate({
                    to: '/settings/bank-accounts/$id',
                    params: { id: row.hit.id },
                });
                break;
            case 'journalEntry':
                void navigate({ to: '/journal/$id', params: { id: row.hit.id } });
                break;
            case 'page':
                void navigate({ to: row.hit.route });
                break;
        }
    }

    function onKeyDown(e: React.KeyboardEvent) {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (rows.length > 0) setActiveIndex(i => (i + 1) % rows.length);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (rows.length > 0) setActiveIndex(i => (i - 1 + rows.length) % rows.length);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            const row = rows[effectiveIndex];
            if (row) navigateTo(row);
        }
    }

    return (
        <dialog
            ref={dialogRef}
            onClose={onClose}
            onCancel={onClose}
            aria-label="Search"
            className={cx(
                'p-0 bg-transparent text-fg-1 mt-[12vh] mx-auto',
                'backdrop:bg-surface-overlay backdrop:backdrop-blur-sm',
            )}
        >
            <div
                className={cx(
                    'w-[calc(100vw-32px)] max-w-[560px] flex flex-col',
                    'bg-bg-1 border border-border-soft rounded-md shadow-overlay',
                )}
                onKeyDown={onKeyDown}
            >
                <div className="flex items-center gap-2 px-4 py-3 border-b border-border-soft">
                    <Icon name="search" size={16} strokeWidth={1.75} className="text-fg-3" />
                    <input
                        ref={inputRef}
                        type="search"
                        value={query}
                        onChange={e => {
                            setQuery(e.target.value);
                            setActiveIndex(0);
                        }}
                        placeholder="Search…"
                        aria-label="Search query"
                        className="flex-1 bg-transparent outline-none text-14 text-fg-1 placeholder:text-fg-3"
                    />
                    <kbd className="px-1.5 py-0.5 rounded bg-surface-2 text-11 text-fg-3 tabular border border-border-soft">
                        Esc
                    </kbd>
                </div>
                <div className="max-h-[60vh] overflow-y-auto py-2">
                    <LauncherBody
                        query={debounced}
                        isPending={search.isPending && search.fetchStatus !== 'idle'}
                        data={search.data ?? null}
                        rows={rows}
                        activeIndex={effectiveIndex}
                        onPick={navigateTo}
                        onHover={setActiveIndex}
                    />
                </div>
            </div>
        </dialog>
    );
}

function LauncherBody({
    query,
    isPending,
    data,
    rows,
    activeIndex,
    onPick,
    onHover,
}: {
    query: string;
    isPending: boolean;
    data: ReturnType<typeof useSearch>['data'] | null;
    rows: FlatRow[];
    activeIndex: number;
    onPick: (row: FlatRow) => void;
    onHover: (index: number) => void;
}) {
    if (query.length < 2) {
        return (
            <p className="px-4 py-6 text-center text-12 text-fg-3">
                Type to search accounts, counterparties, bank accounts, journal entries.
            </p>
        );
    }

    if (isPending || !data) {
        return <p className="px-4 py-6 text-center text-12 text-fg-3">Searching…</p>;
    }

    if (rows.length === 0) {
        return <p className="px-4 py-6 text-center text-12 text-fg-3">No matches for “{query}”.</p>;
    }

    // Track the running flat-index as we render each section so that each row
    // knows whether it's the active one.
    let runningIndex = 0;
    return (
        <>
            <Section
                title="Accounts"
                shown={data.accounts.items.length}
                total={data.accounts.totalCount}
            >
                {data.accounts.items.map(hit => {
                    const i = runningIndex++;
                    return (
                        <Row
                            key={hit.id}
                            active={i === activeIndex}
                            onPick={() => {
                                onPick({ kind: 'account', hit });
                            }}
                            onHover={() => {
                                onHover(i);
                            }}
                            primary={hit.name}
                            secondary={hit.accountType}
                            icon="wallet"
                        />
                    );
                })}
            </Section>
            <Section
                title="Counterparties"
                shown={data.counterparties.items.length}
                total={data.counterparties.totalCount}
            >
                {data.counterparties.items.map(hit => {
                    const i = runningIndex++;
                    return (
                        <Row
                            key={hit.id}
                            active={i === activeIndex}
                            onPick={() => {
                                onPick({ kind: 'counterparty', hit });
                            }}
                            onHover={() => {
                                onHover(i);
                            }}
                            primary={hit.name}
                            icon="user"
                        />
                    );
                })}
            </Section>
            <Section
                title="Bank accounts"
                shown={data.bankAccounts.items.length}
                total={data.bankAccounts.totalCount}
            >
                {data.bankAccounts.items.map(hit => {
                    const i = runningIndex++;
                    return (
                        <Row
                            key={hit.id}
                            active={i === activeIndex}
                            onPick={() => {
                                onPick({ kind: 'bankAccount', hit });
                            }}
                            onHover={() => {
                                onHover(i);
                            }}
                            primary={
                                hit.bankName ??
                                hit.iban ??
                                hit.accountNumber ??
                                hit.cardIdentifier ??
                                'Bank account'
                            }
                            secondary={hit.iban ?? hit.accountNumber ?? hit.cardIdentifier ?? null}
                            icon="bank"
                        />
                    );
                })}
            </Section>
            <Section
                title="Journal entries"
                shown={data.journalEntries.items.length}
                total={data.journalEntries.totalCount}
            >
                {data.journalEntries.items.map(hit => {
                    const i = runningIndex++;
                    return (
                        <Row
                            key={hit.id}
                            active={i === activeIndex}
                            onPick={() => {
                                onPick({ kind: 'journalEntry', hit });
                            }}
                            onHover={() => {
                                onHover(i);
                            }}
                            primary={hit.description ?? '(no description)'}
                            secondary={hit.date}
                            icon="book-open"
                        />
                    );
                })}
            </Section>
            <Section title="Pages" shown={data.pages.items.length} total={data.pages.totalCount}>
                {data.pages.items.map(hit => {
                    const i = runningIndex++;
                    return (
                        <Row
                            key={hit.route}
                            active={i === activeIndex}
                            onPick={() => {
                                onPick({ kind: 'page', hit });
                            }}
                            onHover={() => {
                                onHover(i);
                            }}
                            primary={hit.label}
                            secondary={hit.route}
                            icon="arrow-right"
                        />
                    );
                })}
            </Section>
        </>
    );
}

function Section({
    title,
    shown,
    total,
    children,
}: {
    title: string;
    shown: number;
    total: number;
    children: React.ReactNode;
}) {
    if (shown === 0) return null;
    const moreCount = total - shown;
    return (
        <div className="px-2 pb-2">
            <div className="px-2 py-1 text-11 uppercase tracking-wider text-fg-3">{title}</div>
            {children}
            {moreCount > 0 ? (
                <div className="px-2 pt-1 text-11 text-fg-3">
                    + {moreCount} more matching {title.toLowerCase()}
                </div>
            ) : null}
        </div>
    );
}

function Row({
    active,
    primary,
    secondary,
    icon,
    onPick,
    onHover,
}: {
    active: boolean;
    primary: string;
    secondary?: string | null;
    icon: string;
    onPick: () => void;
    onHover: () => void;
}) {
    return (
        <button
            type="button"
            onClick={onPick}
            onMouseMove={onHover}
            className={cx(
                'w-full flex items-center gap-3 px-2 py-2 rounded-sm text-left',
                active ? 'bg-surface-2' : 'hover:bg-surface-2',
            )}
        >
            <Icon name={icon} size={14} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span className="flex-1 min-w-0 truncate text-13 text-fg-1">{primary}</span>
            {secondary ? (
                <span className="text-12 text-fg-3 tabular truncate max-w-[40%]">{secondary}</span>
            ) : null}
        </button>
    );
}
