import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useBankAccounts, type BankAccount } from '../api/bankAccounts';
import {
    useBankTransaction,
    useCategorizeBankTransaction,
    type BankTransaction,
} from '../api/bankTransactions';
import {
    useCounterparties,
    useSuggestedCounterAccounts,
    type Counterparty,
    type SuggestedCounterAccount,
} from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import {
    asAccountId,
    asCounterpartyId,
    type AccountId,
    type AccountType,
    type BankAccountId,
    type BankTransactionId,
    type CounterpartyId,
} from '../lib/domain';
import { ApiError } from '../lib/http';
import { formatMoney } from '../lib/money';
import {
    applySuggestionsToLines,
    buildRequest,
    computeTotals,
    emptyLine,
    formatMagnitudeFor,
    initialForm,
    type CategorizeFormState,
    type FieldErrors,
    type LineInput,
} from './bankTransactionCategorize.state';

const ACCOUNT_TYPE_ORDER: AccountType[] = [
    'Asset',
    'Liability',
    'Income',
    'Expense',
    'Equity',
];

const ACCOUNT_TYPE_LABEL: Record<AccountType, string> = {
    Asset: 'Assets',
    Liability: 'Liabilities',
    Income: 'Income',
    Expense: 'Expenses',
    Equity: 'Equity',
};

function todayIso(): string {
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, '0');
    const d = String(now.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
}

type Props = { id: BankTransactionId };

export function BankTransactionCategorize({ id }: Props) {
    const bt = useBankTransaction(id);
    const accounts = useAccounts();
    const counterparties = useCounterparties();
    const bankAccounts = useBankAccounts();
    const catalog = useCurrencyCatalog();

    if (
        bt.isPending ||
        accounts.isPending ||
        counterparties.isPending ||
        bankAccounts.isPending
    ) {
        return (
            <Panel>
                <Skeleton className="h-6 w-1/3 mb-3" />
                <Skeleton className="h-4 w-1/2" />
            </Panel>
        );
    }

    if (bt.isError) {
        return (
            <Panel>
                <ErrorState
                    message="Couldn't load bank transaction."
                    onRetry={() => void bt.refetch()}
                />
            </Panel>
        );
    }

    if (accounts.isError) {
        return (
            <Panel>
                <ErrorState
                    message="Couldn't load accounts."
                    onRetry={() => void accounts.refetch()}
                />
            </Panel>
        );
    }

    if (counterparties.isError) {
        return (
            <Panel>
                <ErrorState
                    message="Couldn't load counterparties."
                    onRetry={() => void counterparties.refetch()}
                />
            </Panel>
        );
    }

    if (bankAccounts.isError) {
        return (
            <Panel>
                <ErrorState
                    message="Couldn't load bank accounts."
                    onRetry={() => void bankAccounts.refetch()}
                />
            </Panel>
        );
    }

    if (bt.data.journalEntryId !== null || bt.data.dismissedAt !== null) {
        return <NotCategorisableState bt={bt.data} />;
    }

    return (
        <CategorizeForm
            bt={bt.data}
            accounts={accounts.data}
            counterparties={counterparties.data}
            bankAccounts={bankAccounts.data}
            catalog={catalog}
        />
    );
}

function NotCategorisableState({ bt }: { bt: BankTransaction }) {
    const reason = bt.journalEntryId
        ? 'This row already has a journal entry.'
        : 'This row is dismissed. Undismiss it first to categorise.';
    return (
        <Panel>
            <SectionHead
                title="Categorise bank transaction"
                action={
                    <Link
                        to="/bank-transactions"
                        search={{ page: 1, filter: 'Inbox' }}
                        className="text-[12px] text-fg-3 hover:text-fg-1"
                    >
                        ← Back to inbox
                    </Link>
                }
            />
            <div className="py-6 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">{reason}</span>
            </div>
        </Panel>
    );
}

function CategorizeForm({
    bt,
    accounts,
    counterparties,
    bankAccounts,
    catalog,
}: {
    bt: BankTransaction;
    accounts: Account[];
    counterparties: Counterparty[];
    bankAccounts: BankAccount[];
    catalog: CurrencyCatalog;
}) {
    const categorize = useCategorizeBankTransaction();
    const toast = useToast();
    const navigate = useNavigate();

    const resolvedCounterpartyId = useMemo(
        () => resolveCounterpartyByIban(bt.counterpartyAccountNumber, bankAccounts),
        [bt.counterpartyAccountNumber, bankAccounts],
    );

    const scale = useMemo(() => {
        const currency = catalog.get(bt.money.currencyCode);
        return currency?.minorUnitScale ?? 2;
    }, [catalog, bt.money.currencyCode]);

    const formatMagnitude = useMemo(() => formatMagnitudeFor(scale), [scale]);

    const [form, setForm] = useState<CategorizeFormState>(() =>
        initialForm({
            today: todayIso(),
            bookingDate: bt.bookingDate,
            description: bt.description,
            resolvedCounterpartyId,
            btAmountMinor: bt.money.amount,
            formatMagnitude,
        }),
    );
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<FieldErrors | null>(null);

    const accountsById = useMemo(() => {
        const m = new Map<AccountId, Account>();
        for (const a of accounts) m.set(a.id, a);
        return m;
    }, [accounts]);

    const activeCounterpartyId =
        form.counterpartyMode === 'existing' ? form.counterpartyId : null;
    const suggestions = useSuggestedCounterAccounts(activeCounterpartyId);

    // Track whether the user has interacted with the lines so we don't clobber
    // their edits when suggestions land asynchronously. Pre-fill happens once
    // per counterparty change while the form is still pristine.
    const pristine = useRef(true);
    const lastAppliedCounterpartyId = useRef<CounterpartyId | null>(null);

    useEffect(() => {
        if (activeCounterpartyId === null) {
            lastAppliedCounterpartyId.current = null;
            return;
        }
        if (lastAppliedCounterpartyId.current === activeCounterpartyId) return;
        if (!suggestions.data) return;
        if (!pristine.current) return;
        const next = applySuggestionsToLines(
            filterSuggestionsByCurrency(suggestions.data, accountsById, bt.money.currencyCode),
            bt.money.amount,
            formatMagnitude,
        );
        setForm(prev => ({ ...prev, lines: next }));
        lastAppliedCounterpartyId.current = activeCounterpartyId;
    }, [
        activeCounterpartyId,
        suggestions.data,
        bt.money.amount,
        bt.money.currencyCode,
        accountsById,
        formatMagnitude,
    ]);

    function setLines(updater: (lines: LineInput[]) => LineInput[]) {
        pristine.current = false;
        setForm(prev => ({ ...prev, lines: updater(prev.lines) }));
    }

    function updateLine(index: number, patch: Partial<LineInput>) {
        setLines(prev => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)));
    }

    function addLine() {
        setLines(prev => [...prev, emptyLine()]);
    }

    function removeLine(index: number) {
        setLines(prev => {
            const next = prev.filter((_, i) => i !== index);
            return next.length === 0 ? [emptyLine()] : next;
        });
    }

    const totals = computeTotals(form.lines, bt.money.amount, scale);

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        const result = buildRequest(form, bt.money.amount, scale);
        if (!result.ok) {
            setFieldErrors(result.fieldErrors);
            if (result.topError) setTopError(result.topError);
            return;
        }
        try {
            const created = await categorize.mutateAsync({ id: bt.id, request: result.request });
            toast.success('Categorised.');
            await navigate({ to: '/journal/$id', params: { id: created.id } });
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
                    title="Categorise bank transaction"
                    subtitle="Turn this bank row into a journal entry."
                    action={
                        <Link
                            to="/bank-transactions"
                            search={{ page: 1, filter: 'Inbox' }}
                            className="text-[12px] text-fg-3 hover:text-fg-1"
                        >
                            ← Cancel
                        </Link>
                    }
                />
                <BankTransactionSummary bt={bt} catalog={catalog} />
                <FormErrorBanner message={topError} />
                <HeaderInputs
                    form={form}
                    counterparties={counterparties}
                    onPatch={patch => {
                        setForm(prev => ({ ...prev, ...patch }));
                    }}
                    fieldErrors={fieldErrors}
                />
            </Panel>

            <Panel>
                <Lines
                    lines={form.lines}
                    accounts={accounts}
                    bankAccounts={bankAccounts}
                    bankTransactionBankAccountId={bt.bankAccountId}
                    currencyCode={bt.money.currencyCode}
                    fieldErrors={fieldErrors}
                    onUpdate={updateLine}
                    onAdd={addLine}
                    onRemove={removeLine}
                />
                <UnallocatedFooter
                    totals={totals}
                    currencyCode={bt.money.currencyCode}
                    catalog={catalog}
                />
                <div className="flex items-center justify-end gap-2 mt-4 pt-3 border-t border-border-soft">
                    <Link
                        to="/bank-transactions"
                        search={{ page: 1, filter: 'Inbox' }}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1"
                    >
                        Cancel
                    </Link>
                    <button
                        type="submit"
                        disabled={categorize.isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {categorize.isPending ? 'Categorising…' : 'Categorise'}
                    </button>
                </div>
            </Panel>
        </form>
    );
}

function BankTransactionSummary({
    bt,
    catalog,
}: {
    bt: BankTransaction;
    catalog: CurrencyCatalog;
}) {
    const colour = bt.money.amount < 0 ? 'text-danger' : 'text-success';
    return (
        <div className="grid grid-cols-[100px_1fr_minmax(160px,1.2fr)_160px] gap-3 items-baseline px-3 py-2 mb-4 rounded-sm bg-surface-2 border border-border-soft text-[13px]">
            <span className="text-fg-3 tabular">{bt.bookingDate}</span>
            <span className="text-fg-1 truncate">{bt.description}</span>
            <div className="min-w-0 flex flex-col leading-tight">
                <span className="text-fg-2 truncate">{bt.counterpartyName ?? '—'}</span>
                {bt.counterpartyAccountNumber && (
                    <span className="text-[11px] text-fg-3 truncate tabular">
                        {bt.counterpartyAccountNumber}
                    </span>
                )}
            </div>
            <span className={cx('font-mono tabular text-right', colour)}>
                {formatMoney(bt.money.amount, bt.money.currencyCode, catalog, { sign: true })}
            </span>
        </div>
    );
}

function HeaderInputs({
    form,
    counterparties,
    onPatch,
    fieldErrors,
}: {
    form: CategorizeFormState;
    counterparties: Counterparty[];
    onPatch: (patch: Partial<CategorizeFormState>) => void;
    fieldErrors: FieldErrors | null;
}) {
    return (
        <div className="grid grid-cols-[140px_1fr_minmax(220px,300px)] gap-3">
            <label className="flex flex-col gap-1">
                <span className="text-[12px] font-medium text-fg-2">Date</span>
                <input
                    type="date"
                    value={form.date}
                    onChange={e => {
                        onPatch({ date: e.target.value });
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
                    value={form.description}
                    onChange={e => {
                        onPatch({ description: e.target.value });
                    }}
                    maxLength={500}
                    placeholder="Optional"
                    className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                />
                <FieldError name="Description" errors={fieldErrors} />
            </label>
            <CounterpartyInput
                form={form}
                counterparties={counterparties}
                onPatch={onPatch}
                fieldErrors={fieldErrors}
            />
        </div>
    );
}

function CounterpartyInput({
    form,
    counterparties,
    onPatch,
    fieldErrors,
}: {
    form: CategorizeFormState;
    counterparties: Counterparty[];
    onPatch: (patch: Partial<CategorizeFormState>) => void;
    fieldErrors: FieldErrors | null;
}) {
    const sorted = useMemo(
        () => [...counterparties].sort((a, b) => a.name.localeCompare(b.name)),
        [counterparties],
    );

    return (
        <div className="flex flex-col gap-1">
            <span className="text-[12px] font-medium text-fg-2">Counterparty</span>
            {form.counterpartyMode === 'existing' ? (
                <div className="flex items-stretch gap-2">
                    <select
                        value={form.counterpartyId ?? ''}
                        onChange={e => {
                            onPatch({
                                counterpartyId:
                                    e.target.value === ''
                                        ? null
                                        : asCounterpartyId(e.target.value),
                            });
                        }}
                        className="flex-1 min-w-0 px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                    >
                        <option value="">None (self-transfer)</option>
                        {sorted.map(c => (
                            <option key={c.id} value={c.id}>
                                {c.name}
                            </option>
                        ))}
                    </select>
                    <button
                        type="button"
                        onClick={() => {
                            onPatch({
                                counterpartyMode: 'new',
                                counterpartyId: null,
                            });
                        }}
                        title="Create a new counterparty"
                        className="shrink-0 inline-flex items-center gap-1 px-2 rounded-sm bg-surface-2 border border-border-soft text-fg-2 hover:text-fg-1 hover:border-border-strong text-[13px]"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        New
                    </button>
                </div>
            ) : (
                <div className="flex items-stretch gap-2">
                    <input
                        type="text"
                        value={form.newCounterpartyName}
                        onChange={e => {
                            onPatch({ newCounterpartyName: e.target.value });
                        }}
                        maxLength={200}
                        autoFocus
                        placeholder="New counterparty name"
                        className="flex-1 min-w-0 px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                    />
                    <button
                        type="button"
                        onClick={() => {
                            onPatch({
                                counterpartyMode: 'existing',
                                newCounterpartyName: '',
                            });
                        }}
                        title="Pick an existing counterparty"
                        className="shrink-0 inline-flex items-center gap-1 px-2 rounded-sm bg-surface-2 border border-border-soft text-fg-2 hover:text-fg-1 hover:border-border-strong text-[13px]"
                    >
                        Pick
                    </button>
                </div>
            )}
            <FieldError
                name={form.counterpartyMode === 'existing' ? 'CounterpartyId' : 'NewCounterparty.Name'}
                errors={fieldErrors}
            />
        </div>
    );
}

function Lines({
    lines,
    accounts,
    bankAccounts,
    bankTransactionBankAccountId,
    currencyCode,
    fieldErrors,
    onUpdate,
    onAdd,
    onRemove,
}: {
    lines: LineInput[];
    accounts: Account[];
    bankAccounts: BankAccount[];
    bankTransactionBankAccountId: BankAccountId;
    currencyCode: string;
    fieldErrors: FieldErrors | null;
    onUpdate: (index: number, patch: Partial<LineInput>) => void;
    onAdd: () => void;
    onRemove: (index: number) => void;
}) {
    // Only exclude the BT's own bank-side account (otherwise the JE would
    // credit and debit the same account, since the server adds that line).
    // Other user-owned accounts must remain pickable so self-transfers
    // (Current → Savings, Current → Credit Card) work — see ADR 0014(e).
    const ownBankSideAccountId = useMemo(() => {
        const ba = bankAccounts.find(b => b.id === bankTransactionBankAccountId);
        return ba?.accountId ?? null;
    }, [bankAccounts, bankTransactionBankAccountId]);

    const visibleAccounts = useMemo(
        () =>
            accounts.filter(
                a => a.currencyCode === currencyCode && a.id !== ownBankSideAccountId,
            ),
        [accounts, currencyCode, ownBankSideAccountId],
    );

    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[1fr_140px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Account</span>
                <span className="text-right">Amount</span>
                <span>Description</span>
                <span />
            </div>
            {lines.map((line, i) => (
                <LineRow
                    key={line.id}
                    line={line}
                    index={i}
                    canRemove={lines.length > 1}
                    accounts={visibleAccounts}
                    fieldErrors={fieldErrors}
                    onUpdate={onUpdate}
                    onRemove={onRemove}
                />
            ))}
            <FieldError name="Lines" errors={fieldErrors} />
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

function LineRow({
    line,
    index,
    canRemove,
    accounts,
    fieldErrors,
    onUpdate,
    onRemove,
}: {
    line: LineInput;
    index: number;
    canRemove: boolean;
    accounts: Account[];
    fieldErrors: FieldErrors | null;
    onUpdate: (index: number, patch: Partial<LineInput>) => void;
    onRemove: (index: number) => void;
}) {
    return (
        <div className="grid grid-cols-[1fr_140px_minmax(140px,1fr)_32px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0">
            <div className="flex flex-col gap-1">
                <AccountPicker
                    value={line.accountId}
                    accounts={accounts}
                    onChange={accountId => {
                        onUpdate(index, { accountId });
                    }}
                />
                <FieldError
                    name={`lines[${index.toString()}].accountId`}
                    errors={fieldErrors}
                />
            </div>
            <div className="flex flex-col gap-1">
                <input
                    type="text"
                    inputMode="decimal"
                    value={line.amount}
                    onChange={e => {
                        onUpdate(index, { amount: e.target.value });
                    }}
                    placeholder="0.00"
                    className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] text-right font-mono tabular focus:outline-none focus:border-border-strong"
                />
                <FieldError
                    name={`lines[${index.toString()}].amount`}
                    errors={fieldErrors}
                />
            </div>
            <div>
                <input
                    type="text"
                    value={line.description}
                    onChange={e => {
                        onUpdate(index, { description: e.target.value });
                    }}
                    maxLength={500}
                    placeholder="Optional"
                    className="w-full px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] focus:outline-none focus:border-border-strong"
                />
            </div>
            <button
                type="button"
                onClick={() => {
                    onRemove(index);
                }}
                disabled={!canRemove}
                title="Remove this line"
                className="self-start mt-[6px] p-1 text-fg-3 hover:text-danger disabled:opacity-40 disabled:cursor-not-allowed"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function AccountPicker({
    value,
    accounts,
    onChange,
}: {
    value: AccountId | null;
    accounts: Account[];
    onChange: (accountId: AccountId | null) => void;
}) {
    const groups = useMemo(() => {
        const buckets = new Map<AccountType, Account[]>();
        for (const account of accounts) {
            const list = buckets.get(account.type) ?? [];
            list.push(account);
            buckets.set(account.type, list);
        }
        for (const list of buckets.values()) {
            list.sort((a, b) => a.name.localeCompare(b.name));
        }
        return ACCOUNT_TYPE_ORDER.filter(t => buckets.has(t)).map(t => ({
            type: t,
            accounts: buckets.get(t) ?? [],
        }));
    }, [accounts]);

    return (
        <select
            value={value ?? ''}
            onChange={e => {
                const next = e.target.value;
                onChange(next === '' ? null : asAccountId(next));
            }}
            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] focus:outline-none focus:border-border-strong w-full"
        >
            <option value="">Select…</option>
            {groups.map(g => (
                <optgroup key={g.type} label={ACCOUNT_TYPE_LABEL[g.type]}>
                    {g.accounts.map(a => (
                        <option key={a.id} value={a.id}>
                            {a.name}
                        </option>
                    ))}
                </optgroup>
            ))}
        </select>
    );
}

function UnallocatedFooter({
    totals,
    currencyCode,
    catalog,
}: {
    totals: ReturnType<typeof computeTotals>;
    currencyCode: string;
    catalog: CurrencyCatalog;
}) {
    const targetStr = formatMoney(totals.targetMinor, currencyCode, catalog);
    const allocatedStr = formatMoney(totals.allocatedMinor, currencyCode, catalog);
    const unallocatedAbs = Math.abs(totals.unallocatedMinor);
    const unallocatedStr = formatMoney(unallocatedAbs, currencyCode, catalog);
    return (
        <div className="flex items-center justify-end gap-4 mt-3 text-[12px] tabular">
            <span className="text-fg-3">
                Target <span className="font-mono text-fg-1">{targetStr}</span>
            </span>
            <span className="text-fg-3">
                Allocated <span className="font-mono text-fg-1">{allocatedStr}</span>
            </span>
            {totals.balanced ? (
                <span className="inline-flex items-center gap-1 text-success">
                    <Icon name="check-circle" size={12} strokeWidth={2} /> Balanced
                </span>
            ) : (
                <span className="inline-flex items-center gap-1 text-danger">
                    <Icon name="alert-circle" size={12} strokeWidth={2} />
                    {totals.unallocatedMinor > 0 ? 'Unallocated' : 'Over by'} {unallocatedStr}
                </span>
            )}
        </div>
    );
}

function resolveCounterpartyByIban(
    iban: string | null,
    bankAccounts: BankAccount[],
): CounterpartyId | null {
    if (!iban) return null;
    const normalised = iban.replace(/\s+/g, '').toUpperCase();
    for (const ba of bankAccounts) {
        if (!ba.iban) continue;
        if (ba.iban.replace(/\s+/g, '').toUpperCase() !== normalised) continue;
        if (ba.counterpartyId !== null) return ba.counterpartyId;
    }
    return null;
}

function filterSuggestionsByCurrency(
    suggestions: readonly SuggestedCounterAccount[],
    accountsById: Map<AccountId, Account>,
    currencyCode: string,
): SuggestedCounterAccount[] {
    return suggestions.filter(s => accountsById.get(s.accountId)?.currencyCode === currencyCode);
}
