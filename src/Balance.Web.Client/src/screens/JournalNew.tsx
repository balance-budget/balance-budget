import { useMemo, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useCounterparties } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useCreateJournalEntry } from '../api/journalEntries';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import { todayIso } from '../lib/dates';
import {
    ACCOUNT_TYPE_LABEL,
    ACCOUNT_TYPE_ORDER,
    asAccountId,
    asCounterpartyId,
    type AccountId,
    type AccountType,
    type CounterpartyId,
} from '../lib/domain';
import { handleFormError } from '../lib/formErrors';
import { formatMoney } from '../lib/money';
import { CounterpartyFormModal } from './CounterpartyForm';
import {
    advancedStateToCreateRequest,
    computeAdvancedTotals,
    emptyAdvancedLine,
    emptyForm,
    emptySimpleLeg,
    simpleStateToCreateRequest,
    simpleToAdvanced,
    tryAdvancedToSimple,
    type AdvancedLine,
    type FieldErrors,
    type FormState,
    type ScaleLookup,
    type SimpleLeg,
} from './journalNew.state';

export function JournalNew() {
    const accounts = useAccounts();
    const counterparties = useCounterparties();
    const catalog = useCurrencyCatalog();

    if (accounts.isPending || counterparties.isPending) {
        return (
            <Panel>
                <Skeleton className="h-6 w-1/3 mb-3" />
                <Skeleton className="h-4 w-1/2" />
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

    return (
        <JournalNewForm
            accounts={accounts.data}
            counterparties={counterparties.data}
            catalog={catalog}
        />
    );
}

function JournalNewForm({
    accounts,
    counterparties,
    catalog,
}: {
    accounts: Account[];
    counterparties: { id: CounterpartyId; name: string }[];
    catalog: CurrencyCatalog;
}) {
    const create = useCreateJournalEntry();
    const toast = useToast();
    const navigate = useNavigate();

    const [form, setForm] = useState<FormState>(() => emptyForm(todayIso()));
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<FieldErrors | null>(null);
    const [createCounterparty, setCreateCounterparty] = useState(false);

    const accountsById = useMemo(() => {
        const m = new Map<AccountId, Account>();
        for (const a of accounts) m.set(a.id, a);
        return m;
    }, [accounts]);

    const scaleLookup: ScaleLookup = id => {
        const account = accountsById.get(id);
        if (!account) return null;
        const currency = catalog.get(account.currencyCode);
        return currency?.minorUnitScale ?? 2;
    };

    const anchorCurrency = pickAnchorCurrency(form, accountsById);

    function setHeader(patch: Partial<FormState['header']>) {
        setForm(prev => ({ ...prev, header: { ...prev.header, ...patch } }));
    }

    function setSimple(updater: (s: FormState['simple']) => FormState['simple']) {
        setForm(prev => ({ ...prev, simple: updater(prev.simple) }));
    }

    function setAdvanced(updater: (a: AdvancedLine[]) => AdvancedLine[]) {
        setForm(prev => ({ ...prev, advanced: updater(prev.advanced) }));
    }

    function switchToAdvanced() {
        setForm(prev => ({
            ...prev,
            mode: 'advanced',
            advanced: simpleToAdvanced(prev.simple),
        }));
    }

    function switchToSimple() {
        setForm(prev => {
            const simple = tryAdvancedToSimple(prev.advanced);
            if (!simple) return prev;
            return { ...prev, mode: 'simple', simple };
        });
    }

    const canSwitchToSimple =
        form.mode === 'advanced' ? tryAdvancedToSimple(form.advanced) !== null : true;

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        const result =
            form.mode === 'simple'
                ? simpleStateToCreateRequest(form.simple, form.header, scaleLookup)
                : advancedStateToCreateRequest(form.advanced, form.header, scaleLookup);
        if (!result.ok) {
            setFieldErrors(result.fieldErrors);
            if (result.topError) setTopError(result.topError);
            return;
        }
        try {
            const created = await create.mutateAsync(result.request);
            toast.success('Journal entry created.');
            await navigate({ to: '/journal/$id', params: { id: created.id } });
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <>
            <form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                noValidate
            >
                <Panel>
                    <SectionHead
                        title="New journal entry"
                        subtitle={
                            form.mode === 'simple'
                                ? 'Money moved from one or more sources to one or more destinations.'
                                : 'General-journal table — explicit debit / credit per line.'
                        }
                        action={
                            <Link
                                to="/activity"
                                search={{ page: 1, q: '' }}
                                className="text-[12px] text-fg-3 hover:text-fg-1"
                            >
                                ← Cancel
                            </Link>
                        }
                    />
                    <FormErrorBanner message={topError} />
                    <HeaderInputs
                        form={form}
                        counterparties={counterparties}
                        onPatch={setHeader}
                        onAddCounterparty={() => {
                            setCreateCounterparty(true);
                        }}
                        fieldErrors={fieldErrors}
                    />
                    <ModeToggle
                        mode={form.mode}
                        canSwitchToSimple={canSwitchToSimple}
                        onSwitchSimple={switchToSimple}
                        onSwitchAdvanced={switchToAdvanced}
                    />
                </Panel>

                <Panel>
                    {form.mode === 'simple' ? (
                        <SimpleLines
                            simple={form.simple}
                            accounts={accounts}
                            accountsById={accountsById}
                            anchorCurrency={anchorCurrency}
                            fieldErrors={fieldErrors}
                            onChange={setSimple}
                        />
                    ) : (
                        <AdvancedLines
                            advanced={form.advanced}
                            accounts={accounts}
                            accountsById={accountsById}
                            anchorCurrency={anchorCurrency}
                            catalog={catalog}
                            scaleLookup={scaleLookup}
                            fieldErrors={fieldErrors}
                            onChange={setAdvanced}
                        />
                    )}
                    <div className="flex items-center justify-end gap-2 mt-4 pt-3 border-t border-border-soft">
                        <Link
                            to="/activity"
                            search={{ page: 1, q: '' }}
                            className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1"
                        >
                            Cancel
                        </Link>
                        <button
                            type="submit"
                            disabled={create.isPending}
                            className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                        >
                            {create.isPending ? 'Creating…' : 'Create entry'}
                        </button>
                    </div>
                </Panel>
            </form>

            {createCounterparty && (
                <CounterpartyFormModal
                    mode="create"
                    onClose={() => {
                        setCreateCounterparty(false);
                    }}
                />
            )}
        </>
    );
}

function HeaderInputs({
    form,
    counterparties,
    onPatch,
    onAddCounterparty,
    fieldErrors,
}: {
    form: FormState;
    counterparties: { id: CounterpartyId; name: string }[];
    onPatch: (patch: Partial<FormState['header']>) => void;
    onAddCounterparty: () => void;
    fieldErrors: FieldErrors | null;
}) {
    return (
        <div className="grid grid-cols-[140px_1fr_minmax(220px,300px)] gap-3 mb-4">
            <label className="flex flex-col gap-1">
                <span className="text-[12px] font-medium text-fg-2">Date</span>
                <input
                    type="date"
                    value={form.header.date}
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
                    value={form.header.description}
                    onChange={e => {
                        onPatch({ description: e.target.value });
                    }}
                    maxLength={500}
                    placeholder="Optional"
                    className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                />
                <FieldError name="Description" errors={fieldErrors} />
            </label>
            <div className="flex flex-col gap-1">
                <span className="text-[12px] font-medium text-fg-2">Counterparty</span>
                <div className="flex items-stretch gap-2">
                    <select
                        value={form.header.counterpartyId ?? ''}
                        onChange={e => {
                            onPatch({
                                counterpartyId:
                                    e.target.value === '' ? null : asCounterpartyId(e.target.value),
                            });
                        }}
                        className="flex-1 min-w-0 px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                    >
                        <option value="">None</option>
                        {counterparties.map(c => (
                            <option key={c.id} value={c.id}>
                                {c.name}
                            </option>
                        ))}
                    </select>
                    <button
                        type="button"
                        onClick={onAddCounterparty}
                        title="Create a new counterparty"
                        className="shrink-0 inline-flex items-center gap-1 px-2 rounded-sm bg-surface-2 border border-border-soft text-fg-2 hover:text-fg-1 hover:border-border-strong text-[13px]"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        New
                    </button>
                </div>
                <FieldError name="CounterpartyId" errors={fieldErrors} />
            </div>
        </div>
    );
}

function ModeToggle({
    mode,
    canSwitchToSimple,
    onSwitchSimple,
    onSwitchAdvanced,
}: {
    mode: FormState['mode'];
    canSwitchToSimple: boolean;
    onSwitchSimple: () => void;
    onSwitchAdvanced: () => void;
}) {
    return (
        <div className="inline-flex items-center gap-1 p-[2px] rounded-sm bg-surface-2 border border-border-soft text-[12px] font-medium">
            <button
                type="button"
                onClick={onSwitchSimple}
                disabled={!canSwitchToSimple}
                title={
                    canSwitchToSimple
                        ? 'Switch to the personal-finance shape'
                        : 'Multi-source-multi-destination entries can only be edited in Advanced.'
                }
                className={cx(
                    'px-2 py-1 rounded-sm',
                    mode === 'simple'
                        ? 'bg-surface-1 text-fg-1'
                        : 'text-fg-3 hover:text-fg-1 disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:text-fg-3',
                )}
            >
                Simple
            </button>
            <button
                type="button"
                onClick={onSwitchAdvanced}
                className={cx(
                    'px-2 py-1 rounded-sm',
                    mode === 'advanced' ? 'bg-surface-1 text-fg-1' : 'text-fg-3 hover:text-fg-1',
                )}
            >
                Advanced
            </button>
        </div>
    );
}

function pickAnchorCurrency(form: FormState, accountsById: Map<AccountId, Account>): string | null {
    const legs = form.mode === 'simple' ? [...form.simple.from, ...form.simple.to] : form.advanced;
    for (const leg of legs) {
        if (leg.accountId === null) continue;
        const account = accountsById.get(leg.accountId);
        if (account) return account.currencyCode;
    }
    return null;
}

function SimpleLines({
    simple,
    accounts,
    accountsById,
    anchorCurrency,
    fieldErrors,
    onChange,
}: {
    simple: FormState['simple'];
    accounts: Account[];
    accountsById: Map<AccountId, Account>;
    anchorCurrency: string | null;
    fieldErrors: FieldErrors | null;
    onChange: (updater: (s: FormState['simple']) => FormState['simple']) => void;
}) {
    function updateLeg(side: 'from' | 'to', index: number, patch: Partial<SimpleLeg>) {
        onChange(prev => ({
            ...prev,
            [side]: prev[side].map((leg, i) => (i === index ? { ...leg, ...patch } : leg)),
        }));
    }

    function addLeg(side: 'from' | 'to') {
        onChange(prev => ({ ...prev, [side]: [...prev[side], emptySimpleLeg()] }));
    }

    function removeLeg(side: 'from' | 'to', index: number) {
        onChange(prev => {
            const next = prev[side].filter((_, i) => i !== index);
            return { ...prev, [side]: next.length === 0 ? [emptySimpleLeg()] : next };
        });
    }

    return (
        <div className="grid grid-cols-2 gap-6">
            <SimpleLegColumn
                heading="From"
                subheading="Money leaves these accounts (credit)."
                side="from"
                legs={simple.from}
                accounts={accounts}
                anchorCurrency={anchorCurrency}
                accountsById={accountsById}
                fieldErrors={fieldErrors}
                onChange={updateLeg}
                onAdd={() => {
                    addLeg('from');
                }}
                onRemove={i => {
                    removeLeg('from', i);
                }}
            />
            <SimpleLegColumn
                heading="To"
                subheading="Money arrives in these accounts (debit)."
                side="to"
                legs={simple.to}
                accounts={accounts}
                anchorCurrency={anchorCurrency}
                accountsById={accountsById}
                fieldErrors={fieldErrors}
                onChange={updateLeg}
                onAdd={() => {
                    addLeg('to');
                }}
                onRemove={i => {
                    removeLeg('to', i);
                }}
            />
        </div>
    );
}

function SimpleLegColumn({
    heading,
    subheading,
    side,
    legs,
    accounts,
    anchorCurrency,
    accountsById,
    fieldErrors,
    onChange,
    onAdd,
    onRemove,
}: {
    heading: string;
    subheading: string;
    side: 'from' | 'to';
    legs: SimpleLeg[];
    accounts: Account[];
    anchorCurrency: string | null;
    accountsById: Map<AccountId, Account>;
    fieldErrors: FieldErrors | null;
    onChange: (side: 'from' | 'to', index: number, patch: Partial<SimpleLeg>) => void;
    onAdd: () => void;
    onRemove: (index: number) => void;
}) {
    return (
        <div className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between">
                <div className="flex flex-col gap-[2px]">
                    <h3 className="text-[13px] font-semibold text-fg-1">{heading}</h3>
                    <span className="text-[11px] text-fg-3">{subheading}</span>
                </div>
                <button
                    type="button"
                    onClick={onAdd}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                >
                    <Icon name="plus" size={12} strokeWidth={2} />
                    Add
                </button>
            </div>
            {legs.map((leg, i) => {
                const legAccount = leg.accountId ? accountsById.get(leg.accountId) : null;
                const filterCurrency =
                    anchorCurrency && legAccount?.currencyCode === anchorCurrency
                        ? null
                        : anchorCurrency;
                return (
                    <div key={leg.id} className="flex items-start gap-2">
                        <div className="flex-1 min-w-0 flex flex-col gap-1">
                            <AccountPicker
                                value={leg.accountId}
                                accounts={accounts}
                                filterCurrency={filterCurrency}
                                onChange={accountId => {
                                    onChange(side, i, { accountId });
                                }}
                            />
                            <FieldError
                                name={`simple.${side}[${i.toString()}].accountId`}
                                errors={fieldErrors}
                            />
                        </div>
                        <div className="w-[140px] shrink-0 flex flex-col gap-1">
                            <input
                                type="text"
                                inputMode="decimal"
                                value={leg.amount}
                                onChange={e => {
                                    onChange(side, i, { amount: e.target.value });
                                }}
                                placeholder="0.00"
                                className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] text-right font-mono tabular focus:outline-none focus:border-border-strong"
                            />
                            <FieldError
                                name={`simple.${side}[${i.toString()}].amount`}
                                errors={fieldErrors}
                            />
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                onRemove(i);
                            }}
                            disabled={legs.length === 1}
                            title="Remove this leg"
                            className="shrink-0 mt-[2px] p-2 text-fg-3 hover:text-danger disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                        </button>
                    </div>
                );
            })}
        </div>
    );
}

function AdvancedLines({
    advanced,
    accounts,
    accountsById,
    anchorCurrency,
    catalog,
    scaleLookup,
    fieldErrors,
    onChange,
}: {
    advanced: AdvancedLine[];
    accounts: Account[];
    accountsById: Map<AccountId, Account>;
    anchorCurrency: string | null;
    catalog: CurrencyCatalog;
    scaleLookup: ScaleLookup;
    fieldErrors: FieldErrors | null;
    onChange: (updater: (a: AdvancedLine[]) => AdvancedLine[]) => void;
}) {
    function updateLine(index: number, patch: Partial<AdvancedLine>) {
        onChange(prev => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)));
    }

    function addLine() {
        onChange(prev => [...prev, emptyAdvancedLine()]);
    }

    function removeLine(index: number) {
        onChange(prev => {
            const next = prev.filter((_, i) => i !== index);
            return next.length < 2 ? [...next, emptyAdvancedLine()] : next;
        });
    }

    const totals = computeAdvancedTotals(advanced, scaleLookup);

    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[1fr_120px_120px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Account</span>
                <span className="text-right">Debit</span>
                <span className="text-right">Credit</span>
                <span>Description</span>
                <span />
            </div>
            {advanced.map((line, i) => {
                const lineAccount = line.accountId ? accountsById.get(line.accountId) : null;
                const filterCurrency =
                    anchorCurrency && lineAccount?.currencyCode === anchorCurrency
                        ? null
                        : anchorCurrency;
                return (
                    <div
                        key={line.id}
                        className="grid grid-cols-[1fr_120px_120px_minmax(140px,1fr)_32px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0"
                    >
                        <div className="flex flex-col gap-1">
                            <AccountPicker
                                value={line.accountId}
                                accounts={accounts}
                                filterCurrency={filterCurrency}
                                onChange={accountId => {
                                    updateLine(i, { accountId });
                                }}
                            />
                            <FieldError
                                name={`advanced[${i.toString()}].accountId`}
                                errors={fieldErrors}
                            />
                        </div>
                        <div className="flex flex-col gap-1">
                            <input
                                type="text"
                                inputMode="decimal"
                                value={line.debit}
                                onChange={e => {
                                    updateLine(i, { debit: e.target.value });
                                }}
                                placeholder="0.00"
                                className="px-2 py-1 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] text-right font-mono tabular focus:outline-none focus:border-border-strong"
                            />
                            <FieldError
                                name={`advanced[${i.toString()}].debit`}
                                errors={fieldErrors}
                            />
                        </div>
                        <div className="flex flex-col gap-1">
                            <input
                                type="text"
                                inputMode="decimal"
                                value={line.credit}
                                onChange={e => {
                                    updateLine(i, { credit: e.target.value });
                                }}
                                placeholder="0.00"
                                className="px-2 py-1 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] text-right font-mono tabular focus:outline-none focus:border-border-strong"
                            />
                            <FieldError
                                name={`advanced[${i.toString()}].credit`}
                                errors={fieldErrors}
                            />
                        </div>
                        <div className="flex flex-col gap-1">
                            <input
                                type="text"
                                value={line.description}
                                onChange={e => {
                                    updateLine(i, { description: e.target.value });
                                }}
                                maxLength={500}
                                placeholder="Optional"
                                className="px-2 py-1 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px] focus:outline-none focus:border-border-strong"
                            />
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                removeLine(i);
                            }}
                            disabled={advanced.length <= 2}
                            title="Remove this line"
                            className="self-start mt-[2px] p-1 text-fg-3 hover:text-danger disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                        </button>
                    </div>
                );
            })}
            <div className="flex items-center justify-between mt-2">
                <button
                    type="button"
                    onClick={addLine}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-sm text-[12px] text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                >
                    <Icon name="plus" size={12} strokeWidth={2} />
                    Add line
                </button>
                <AdvancedTotalsFooter totals={totals} currency={anchorCurrency} catalog={catalog} />
            </div>
        </div>
    );
}

function AdvancedTotalsFooter({
    totals,
    currency,
    catalog,
}: {
    totals: ReturnType<typeof computeAdvancedTotals>;
    currency: string | null;
    catalog: CurrencyCatalog;
}) {
    if (!currency) {
        return <span className="text-[12px] text-fg-3">Σ Debit / Credit</span>;
    }
    const debitStr = formatMoney(totals.debitMinor, currency, catalog);
    const creditStr = formatMoney(totals.creditMinor, currency, catalog);
    const diff = totals.debitMinor - totals.creditMinor;
    return (
        <div className="flex items-center gap-4 text-[12px] tabular">
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
                    Off by {formatMoney(Math.abs(diff), currency, catalog)}
                </span>
            )}
        </div>
    );
}

function AccountPicker({
    value,
    accounts,
    filterCurrency,
    onChange,
}: {
    value: AccountId | null;
    accounts: Account[];
    filterCurrency: string | null;
    onChange: (accountId: AccountId | null) => void;
}) {
    const groups = useMemo(() => {
        const visible = filterCurrency
            ? accounts.filter(a => a.currencyCode === filterCurrency || a.id === value)
            : accounts;
        const buckets = new Map<AccountType, Account[]>();
        for (const account of visible) {
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
    }, [accounts, filterCurrency, value]);

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
                            {a.name} — {a.currencyCode}
                        </option>
                    ))}
                </optgroup>
            ))}
        </select>
    );
}
