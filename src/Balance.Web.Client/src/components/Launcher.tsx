import { useState } from 'react';
import {
    Autocomplete,
    Dialog,
    Header,
    Input,
    Menu,
    MenuItem,
    type MenuItemProps,
    MenuSection,
    Modal as AriaModal,
    ModalOverlay,
    SearchField,
} from 'react-aria-components';
import { useNavigate } from '@tanstack/react-router';
import { useSearch } from '../api/search';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import { Icon } from './Icon';

type LauncherProps = {
    open: boolean;
    onClose: () => void;
};

/**
 * Cmd-K launcher on React Aria's Autocomplete-in-a-Modal pattern: typeable
 * input at the top, grouped results below, arrow keys + Enter handled by the
 * Autocomplete wiring. Results come from the server (debounced), so no client
 * filter is configured. The hint row shows until the user types 2+ characters.
 */
export function Launcher({ open, onClose }: LauncherProps) {
    const [query, setQuery] = useState('');
    const debounced = useDebouncedValue(query, 200);
    const navigate = useNavigate();
    const search = useSearch(debounced);

    const data = search.data ?? null;
    const isPending = search.isPending && search.fetchStatus !== 'idle';
    const showResults = debounced.length >= 2 && !isPending && data !== null;

    function close() {
        // Reset on the way out — never carry forward state into the next session.
        setQuery('');
        onClose();
    }

    return (
        <ModalOverlay
            isOpen={open}
            onOpenChange={isOpen => {
                if (!isOpen) close();
            }}
            className={
                'fixed inset-0 z-50 flex items-start justify-center pt-[12vh] px-4 overflow-y-auto ' +
                'bg-surface-overlay backdrop-blur-sm ' +
                'data-[entering]:opacity-0 data-[exiting]:opacity-0 transition-opacity duration-fast'
            }
        >
            <AriaModal className="w-[calc(100vw-32px)] max-w-[560px]">
                <Dialog
                    aria-label="Search"
                    className="flex flex-col bg-bg-1 border border-border-soft rounded-md shadow-overlay outline-none text-fg-1"
                >
                    <Autocomplete inputValue={query} onInputChange={setQuery}>
                        <SearchField
                            aria-label="Search query"
                            autoFocus
                            className="flex items-center gap-2 px-4 py-3 border-b border-border-soft"
                        >
                            <Icon
                                name="search"
                                size={16}
                                strokeWidth={1.75}
                                className="text-fg-3"
                            />
                            <Input
                                placeholder="Search…"
                                className={
                                    'flex-1 bg-transparent outline-none text-14 text-fg-1 placeholder:text-fg-3 ' +
                                    '[&::-webkit-search-cancel-button]:appearance-none'
                                }
                            />
                            <kbd className="px-1.5 py-0.5 rounded bg-surface-2 text-11 text-fg-3 tabular border border-border-soft">
                                Esc
                            </kbd>
                        </SearchField>
                        {debounced.length < 2 ? (
                            <Hint>
                                Type to search accounts, counterparties, bank accounts, journal
                                entries.
                            </Hint>
                        ) : isPending || data === null ? (
                            <Hint>Searching…</Hint>
                        ) : null}
                        {showResults && (
                            <Menu
                                aria-label="Search results"
                                className="max-h-[60vh] overflow-y-auto scrollbar-sleek py-2 px-2 outline-none"
                                renderEmptyState={() => (
                                    <p className="px-4 py-6 text-center text-12 text-fg-3">
                                        No matches for “{debounced}”.
                                    </p>
                                )}
                            >
                                <ResultSection
                                    title="Accounts"
                                    shown={data.accounts.items.length}
                                    total={data.accounts.totalCount}
                                >
                                    {data.accounts.items.map(hit => (
                                        <ResultItem
                                            key={hit.id}
                                            id={`account-${hit.id}`}
                                            textValue={hit.name}
                                            icon="wallet"
                                            primary={hit.name}
                                            secondary={hit.accountType}
                                            onAction={() => {
                                                close();
                                                void navigate({
                                                    to: '/accounts/$id',
                                                    params: { id: hit.id },
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
                                            }}
                                        />
                                    ))}
                                </ResultSection>
                                <ResultSection
                                    title="Counterparties"
                                    shown={data.counterparties.items.length}
                                    total={data.counterparties.totalCount}
                                >
                                    {data.counterparties.items.map(hit => (
                                        <ResultItem
                                            key={hit.id}
                                            id={`counterparty-${hit.id}`}
                                            textValue={hit.name}
                                            icon="user"
                                            primary={hit.name}
                                            onAction={() => {
                                                close();
                                                void navigate({
                                                    to: '/counterparties/$id',
                                                    params: { id: hit.id },
                                                    search: { page: 1 },
                                                });
                                            }}
                                        />
                                    ))}
                                </ResultSection>
                                <ResultSection
                                    title="Bank accounts"
                                    shown={data.bankAccounts.items.length}
                                    total={data.bankAccounts.totalCount}
                                >
                                    {data.bankAccounts.items.map(hit => {
                                        const primary =
                                            hit.bankName ??
                                            hit.iban ??
                                            hit.accountNumber ??
                                            hit.cardIdentifier ??
                                            'Bank account';
                                        return (
                                            <ResultItem
                                                key={hit.id}
                                                id={`bank-account-${hit.id}`}
                                                textValue={primary}
                                                icon="bank"
                                                primary={primary}
                                                secondary={
                                                    hit.iban ??
                                                    hit.accountNumber ??
                                                    hit.cardIdentifier ??
                                                    null
                                                }
                                                onAction={() => {
                                                    close();
                                                    void navigate({
                                                        to: '/settings/bank-accounts/$id',
                                                        params: { id: hit.id },
                                                    });
                                                }}
                                            />
                                        );
                                    })}
                                </ResultSection>
                                <ResultSection
                                    title="Journal entries"
                                    shown={data.journalEntries.items.length}
                                    total={data.journalEntries.totalCount}
                                >
                                    {data.journalEntries.items.map(hit => (
                                        <ResultItem
                                            key={hit.id}
                                            id={`journal-${hit.id}`}
                                            textValue={hit.description ?? '(no description)'}
                                            icon="book-open"
                                            primary={hit.description ?? '(no description)'}
                                            secondary={hit.date}
                                            onAction={() => {
                                                close();
                                                void navigate({
                                                    to: '/journal/$id',
                                                    params: { id: hit.id },
                                                });
                                            }}
                                        />
                                    ))}
                                </ResultSection>
                                <ResultSection
                                    title="Pages"
                                    shown={data.pages.items.length}
                                    total={data.pages.totalCount}
                                >
                                    {data.pages.items.map(hit => (
                                        <ResultItem
                                            key={hit.route}
                                            id={`page-${hit.route}`}
                                            textValue={hit.label}
                                            icon="arrow-right"
                                            primary={hit.label}
                                            secondary={hit.route}
                                            onAction={() => {
                                                close();
                                                void navigate({ to: hit.route });
                                            }}
                                        />
                                    ))}
                                </ResultSection>
                            </Menu>
                        )}
                    </Autocomplete>
                </Dialog>
            </AriaModal>
        </ModalOverlay>
    );
}

function Hint({ children }: { children: React.ReactNode }) {
    return <p className="px-4 py-6 text-center text-12 text-fg-3">{children}</p>;
}

function ResultSection({
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
        <MenuSection className="pb-2">
            <Header className="px-2 py-1 text-11 uppercase tracking-wider text-fg-3">
                {title}
            </Header>
            {children}
            {moreCount > 0 ? (
                <MenuItem
                    isDisabled
                    textValue={`${moreCount.toString()} more`}
                    className="px-2 pt-1 text-11 text-fg-3"
                >
                    + {moreCount.toString()} more matching {title.toLowerCase()}
                </MenuItem>
            ) : null}
        </MenuSection>
    );
}

function ResultItem({
    icon,
    primary,
    secondary,
    ...props
}: MenuItemProps & {
    icon: string;
    primary: string;
    secondary?: string | null;
}) {
    return (
        <MenuItem
            {...props}
            className={
                'w-full flex items-center gap-3 px-2 py-2 rounded-sm text-left cursor-pointer outline-none ' +
                'data-[focused]:bg-surface-2 data-[hovered]:bg-surface-2'
            }
        >
            <Icon name={icon} size={14} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span className="flex-1 min-w-0 truncate text-13 text-fg-1">{primary}</span>
            {secondary ? (
                <span className="text-12 text-fg-3 tabular truncate max-w-[40%]">{secondary}</span>
            ) : null}
        </MenuItem>
    );
}
