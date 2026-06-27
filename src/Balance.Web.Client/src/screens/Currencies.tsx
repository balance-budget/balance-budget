import { useMemo, useState } from 'react';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import {
    isCurrencyInUse,
    useCurrencies,
    useDeleteCurrency,
    type Currency,
} from '../api/currencies';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Button } from '../components/ui/Button';
import { SearchField } from '../components/ui/SearchField';
import { useToast } from '../components/ui/Toast';
import { handleActionError } from '../lib/formErrors';
import { CurrencyFormModal } from './CurrencyForm';

export function Currencies() {
    const { t } = useLingui();
    const query = useCurrencies();
    const [q, setQ] = useState('');
    const [creating, setCreating] = useState(false);
    const [editing, setEditing] = useState<Currency | null>(null);
    const [deleting, setDeleting] = useState<Currency | null>(null);

    const currencies = useMemo(() => (query.data ? [...query.data.values()] : []), [query.data]);

    const filtered = useMemo(() => {
        const needle = q.trim().toLowerCase();
        if (needle === '') return currencies;
        return currencies.filter(
            c => c.code.toLowerCase().includes(needle) || c.name.toLowerCase().includes(needle),
        );
    }, [currencies, q]);

    return (
        <>
            <Panel>
                <SectionHead
                    subtitle={t`The currencies your accounts and amounts are denominated in.`}
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg bg-brand-primary text-white text-sm font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            <Trans>New currency</Trans>
                        </button>
                    }
                />
                <div className="mb-4">
                    <SearchField
                        aria-label={t`Search currencies`}
                        value={q}
                        onChange={setQ}
                        placeholder={t`Search by code or name…`}
                    />
                </div>

                {query.isPending ? (
                    <div className="flex flex-col gap-2">
                        <Skeleton className="h-12" />
                        <Skeleton className="h-12" />
                        <Skeleton className="h-12" />
                    </div>
                ) : query.isError ? (
                    <ErrorState
                        message={t`Couldn't load currencies.`}
                        onRetry={() => void query.refetch()}
                    />
                ) : filtered.length === 0 ? (
                    <div className="py-8 text-center text-sm text-fg-2">
                        {q.trim() === '' ? (
                            <Trans>No currencies yet.</Trans>
                        ) : (
                            <Trans>No matches for “{q}”.</Trans>
                        )}
                    </div>
                ) : (
                    <CurrencyTable
                        currencies={filtered}
                        onEdit={setEditing}
                        onDelete={setDeleting}
                    />
                )}
            </Panel>

            {creating && (
                <CurrencyFormModal
                    mode="create"
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
            {editing && (
                <CurrencyFormModal
                    mode="edit"
                    currency={editing}
                    onClose={() => {
                        setEditing(null);
                    }}
                />
            )}
            {deleting && (
                <DeleteCurrencyDialog
                    currency={deleting}
                    onClose={() => {
                        setDeleting(null);
                    }}
                />
            )}
        </>
    );
}

function CurrencyTable({
    currencies,
    onEdit,
    onDelete,
}: {
    currencies: Currency[];
    onEdit: (currency: Currency) => void;
    onDelete: (currency: Currency) => void;
}) {
    return (
        <table className="w-full text-sm">
            <thead>
                <tr className="text-left text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                    <th className="py-2 pr-3 font-medium">
                        <Trans>Code</Trans>
                    </th>
                    <th className="py-2 pr-3 font-medium">
                        <Trans>Name</Trans>
                    </th>
                    <th className="py-2 pr-3 font-medium">
                        <Trans>Symbol</Trans>
                    </th>
                    <th className="py-2 pr-3 font-medium text-right">
                        <Trans>Scale</Trans>
                    </th>
                    <th className="py-2 pr-3 font-medium">
                        <Trans>Usage</Trans>
                    </th>
                    <th className="py-2 font-medium sr-only">
                        <Trans>Actions</Trans>
                    </th>
                </tr>
            </thead>
            <tbody>
                {currencies.map(currency => (
                    <CurrencyRow
                        key={currency.code}
                        currency={currency}
                        onEdit={() => {
                            onEdit(currency);
                        }}
                        onDelete={() => {
                            onDelete(currency);
                        }}
                    />
                ))}
            </tbody>
        </table>
    );
}

function CurrencyRow({
    currency,
    onEdit,
    onDelete,
}: {
    currency: Currency;
    onEdit: () => void;
    onDelete: () => void;
}) {
    const { t } = useLingui();
    const inUse = isCurrencyInUse(currency);

    return (
        <tr className="border-b border-border-soft last:border-b-0 hover:bg-surface-2">
            <td className="py-[10px] pr-3 font-medium text-fg-1 tabular-nums">{currency.code}</td>
            <td className="py-[10px] pr-3 text-fg-1">{currency.name}</td>
            <td className="py-[10px] pr-3 text-fg-2">{currency.symbol ?? '—'}</td>
            <td className="py-[10px] pr-3 text-right text-fg-2 tabular-nums">
                {currency.minorUnitScale}
            </td>
            <td className="py-[10px] pr-3 text-fg-2">
                <CurrencyUsage currency={currency} />
            </td>
            <td className="py-[10px] text-right whitespace-nowrap">
                <Button onPress={onEdit} className="py-[5px] text-xs">
                    <Trans>Edit</Trans>
                </Button>{' '}
                <Button
                    onPress={onDelete}
                    isDisabled={inUse}
                    className="py-[5px] text-xs"
                    {...(inUse
                        ? { 'aria-label': t`In use, can't be deleted while referenced` }
                        : {})}
                >
                    <Trans>Delete</Trans>
                </Button>
            </td>
        </tr>
    );
}

function CurrencyUsage({ currency }: { currency: Currency }) {
    if (!isCurrencyInUse(currency)) {
        return (
            <span className="text-fg-3">
                <Trans>Unused</Trans>
            </span>
        );
    }
    return (
        <span className="tabular-nums">
            {currency.accountCount > 0 && (
                <Plural value={currency.accountCount} one="# account" other="# accounts" />
            )}
            {currency.accountCount > 0 && currency.bankAccountCount > 0 && ', '}
            {currency.bankAccountCount > 0 && (
                <Plural
                    value={currency.bankAccountCount}
                    one="# bank account"
                    other="# bank accounts"
                />
            )}
        </span>
    );
}

function DeleteCurrencyDialog({ currency, onClose }: { currency: Currency; onClose: () => void }) {
    const { t } = useLingui();
    const del = useDeleteCurrency();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(currency.code);
            toast.success(t`Currency deleted.`);
            onClose();
        } catch (err) {
            handleActionError(err, { setError, toast: toast.error });
        }
    }

    return (
        <ConfirmDialog
            open
            onClose={onClose}
            onConfirm={() => void onConfirm()}
            title={t`Delete ${currency.code}?`}
            message={t`You can re-add it later.`}
            confirmLabel={t`Delete`}
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
