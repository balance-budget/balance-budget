import { useMemo, useState } from 'react';
import { Form, ToggleButton, ToggleButtonGroup } from 'react-aria-components';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useCounterparties } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useCreateJournalEntry } from '../api/journalEntries';
import { AccountSelect } from '../components/AccountSelect';
import { DateField } from '../components/DateField';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Button, IconButton } from '../components/ui/Button';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { cx } from '../lib/cx';
import { todayIso } from '../lib/dates';
import { asCounterpartyId, type AccountId, type CounterpartyId } from '../lib/domain';
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

/** Sentinel id for the "None" option — RAC Select keys can't be null. */
const NONE_COUNTERPARTY = '__none__';

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
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
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
                                search={{ page: 1, q: '', account: '', from: '', to: '' }}
                                className="text-12 text-fg-3 hover:text-fg-1"
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
                            accountsById={accountsById}
                            anchorCurrency={anchorCurrency}
                            fieldErrors={fieldErrors}
                            onChange={setSimple}
                        />
                    ) : (
                        <AdvancedLines
                            advanced={form.advanced}
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
                            search={{ page: 1, q: '', account: '', from: '', to: '' }}
                            className="px-3 py-[7px] rounded-sm text-13 font-medium text-fg-2 hover:text-fg-1"
                        >
                            Cancel
                        </Link>
                        <Button type="submit" variant="primary" isDisabled={create.isPending}>
                            {create.isPending ? 'Creating…' : 'Create entry'}
                        </Button>
                    </div>
                </Panel>
            </Form>

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
                <span className="text-12 font-medium text-fg-2">Date</span>
                <DateField
                    value={form.header.date}
                    onChange={date => {
                        onPatch({ date });
                    }}
                    required
                    ariaLabel="Date"
                    className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                />
                <FieldError name="Date" errors={fieldErrors} />
            </label>
            <TextField
                label="Description"
                name="Description"
                value={form.header.description}
                onChange={description => {
                    onPatch({ description });
                }}
                maxLength={500}
                placeholder="Optional"
            />
            <div className="flex flex-col gap-1">
                <span className="text-12 font-medium text-fg-2">Counterparty</span>
                <div className="flex items-stretch gap-2">
                    <Select
                        aria-label="Counterparty"
                        name="CounterpartyId"
                        value={form.header.counterpartyId ?? NONE_COUNTERPARTY}
                        onChange={key => {
                            onPatch({
                                counterpartyId:
                                    key === null || key === NONE_COUNTERPARTY
                                        ? null
                                        : asCounterpartyId(String(key)),
                            });
                        }}
                        className="flex-1 min-w-0"
                    >
                        <SelectItem id={NONE_COUNTERPARTY}>None</SelectItem>
                        {counterparties.map(c => (
                            <SelectItem key={c.id} id={c.id}>
                                {c.name}
                            </SelectItem>
                        ))}
                    </Select>
                    <Button
                        onPress={onAddCounterparty}
                        aria-label="Create a new counterparty"
                        className="shrink-0"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        New
                    </Button>
                </div>
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
    const segmentClass = (selected: boolean) =>
        cx(
            'px-2 py-1 rounded-sm outline-none cursor-pointer data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
            selected
                ? 'bg-surface-1 text-fg-1'
                : 'text-fg-3 data-[hovered]:text-fg-1 data-[disabled]:opacity-40 data-[disabled]:cursor-not-allowed',
        );

    return (
        <ToggleButtonGroup
            selectionMode="single"
            disallowEmptySelection
            selectedKeys={[mode]}
            onSelectionChange={keys => {
                const next = [...keys][0];
                if (next === 'simple') onSwitchSimple();
                if (next === 'advanced') onSwitchAdvanced();
            }}
            className="inline-flex items-center gap-1 p-[2px] rounded-sm bg-surface-2 border border-border-soft text-12 font-medium"
        >
            <ToggleButton
                id="simple"
                isDisabled={!canSwitchToSimple}
                aria-label={
                    canSwitchToSimple
                        ? 'Switch to the personal-finance shape'
                        : 'Multi-source-multi-destination entries can only be edited in Advanced.'
                }
                className={segmentClass(mode === 'simple')}
            >
                Simple
            </ToggleButton>
            <ToggleButton id="advanced" className={segmentClass(mode === 'advanced')}>
                Advanced
            </ToggleButton>
        </ToggleButtonGroup>
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
    accountsById,
    anchorCurrency,
    fieldErrors,
    onChange,
}: {
    simple: FormState['simple'];
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
                    <h3 className="text-13 font-semibold text-fg-1">{heading}</h3>
                    <span className="text-11 text-fg-3">{subheading}</span>
                </div>
                <Button variant="ghost" onPress={onAdd} className="px-2 py-1 text-12 font-normal">
                    <Icon name="plus" size={12} strokeWidth={2} />
                    Add
                </Button>
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
                                className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 text-right font-mono tabular focus:outline-none focus:border-border-strong"
                            />
                            <FieldError
                                name={`simple.${side}[${i.toString()}].amount`}
                                errors={fieldErrors}
                            />
                        </div>
                        <IconButton
                            onPress={() => {
                                onRemove(i);
                            }}
                            isDisabled={legs.length === 1}
                            aria-label="Remove this leg"
                            className="shrink-0 mt-[2px] p-2 data-[hovered]:text-danger data-[hovered]:bg-transparent"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                        </IconButton>
                    </div>
                );
            })}
        </div>
    );
}

function AdvancedLines({
    advanced,
    accountsById,
    anchorCurrency,
    catalog,
    scaleLookup,
    fieldErrors,
    onChange,
}: {
    advanced: AdvancedLine[];
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
            <div className="grid grid-cols-[1fr_120px_120px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-11 text-fg-3 uppercase tracking-wider border-b border-border-soft">
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
                                className="px-2 py-1 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-13 text-right font-mono tabular focus:outline-none focus:border-border-strong"
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
                                className="px-2 py-1 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-13 text-right font-mono tabular focus:outline-none focus:border-border-strong"
                            />
                            <FieldError
                                name={`advanced[${i.toString()}].credit`}
                                errors={fieldErrors}
                            />
                        </div>
                        <TextField
                            aria-label="Line description"
                            value={line.description}
                            onChange={description => {
                                updateLine(i, { description });
                            }}
                            maxLength={500}
                            placeholder="Optional"
                            fieldSize="sm"
                        />
                        <IconButton
                            onPress={() => {
                                removeLine(i);
                            }}
                            isDisabled={advanced.length <= 2}
                            aria-label="Remove this line"
                            className="self-start mt-[2px] data-[hovered]:text-danger data-[hovered]:bg-transparent"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                        </IconButton>
                    </div>
                );
            })}
            <div className="flex items-center justify-between mt-2">
                <Button variant="ghost" onPress={addLine} className="px-2 py-1 text-12 font-normal">
                    <Icon name="plus" size={12} strokeWidth={2} />
                    Add line
                </Button>
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
        return <span className="text-12 text-fg-3">Σ Debit / Credit</span>;
    }
    const debitStr = formatMoney(totals.debitMinor, currency, catalog);
    const creditStr = formatMoney(totals.creditMinor, currency, catalog);
    const diff = totals.debitMinor - totals.creditMinor;
    return (
        <div className="flex items-center gap-4 text-12 tabular">
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
    filterCurrency,
    onChange,
}: {
    value: AccountId | null;
    filterCurrency: string | null;
    onChange: (accountId: AccountId | null) => void;
}) {
    // Journal lines post to leaves only; the currency is anchored to the first
    // chosen leg so every line in the entry shares one currency.
    return (
        <AccountSelect
            value={value}
            onChange={onChange}
            postableOnly
            currencyCode={filterCurrency ?? undefined}
            placeholder="Select…"
            ariaLabel="Account"
        />
    );
}
