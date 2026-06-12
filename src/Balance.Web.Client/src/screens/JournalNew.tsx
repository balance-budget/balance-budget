import { useMemo, useState } from 'react';
import { Form, ToggleButton, ToggleButtonGroup } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useCounterparties, useCreateCounterparty } from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useCreateJournalEntry } from '../api/journalEntries';
import { AccountSelect } from '../components/AccountSelect';
import { ComboBox } from '../components/ui/ComboBox';
import { type ComboBoxItem } from '../components/ui/combobox.state';
import { ErrorState } from '../components/ErrorState';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { usePageHeader } from '../components/PageHeader';
import { Skeleton } from '../components/Skeleton';
import { Button, IconButton } from '../components/ui/Button';
import { DatePicker } from '../components/ui/DatePicker';
import { NumberField } from '../components/ui/NumberField';
import { TextField } from '../components/ui/TextField';
import { selectedKey } from '../components/ui/selection';
import { useToast } from '../components/ui/Toast';
import { cx } from '../lib/cx';
import { todayIso } from '../lib/dates';
import { type AccountId, type CounterpartyId } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';
import { formatMoney } from '../lib/money';
import {
    advancedStateToCreateRequest,
    computeAdvancedTotals,
    emptyAdvancedLine,
    emptyForm,
    emptySimpleLeg,
    prefilledForm,
    simpleStateToCreateRequest,
    simpleToAdvanced,
    tryAdvancedToSimple,
    type AdvancedLine,
    type FieldErrors,
    type FormState,
    type ScaleLookup,
    type SimpleLeg,
} from './journalNew.state';

/** Currency-styled Intl options for amount fields; plain decimal until an
 *  account (and thus a currency) is known. */
function currencyFormat(currencyCode: string | null | undefined): Intl.NumberFormatOptions {
    return currencyCode ? { style: 'currency', currency: currencyCode } : {};
}

export function JournalNew({ prefillAccountId }: { prefillAccountId: AccountId | null }) {
    const { t } = useLingui();
    usePageHeader({ breadcrumb: [{ label: t`Activity`, to: '/activity' }] });
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
                    message={t`Couldn't load accounts.`}
                    onRetry={() => void accounts.refetch()}
                />
            </Panel>
        );
    }

    if (counterparties.isError) {
        return (
            <Panel>
                <ErrorState
                    message={t`Couldn't load counterparties.`}
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
            prefillAccountId={prefillAccountId}
        />
    );
}

function JournalNewForm({
    accounts,
    counterparties,
    catalog,
    prefillAccountId,
}: {
    accounts: Account[];
    counterparties: { id: CounterpartyId; name: string }[];
    catalog: CurrencyCatalog;
    prefillAccountId: AccountId | null;
}) {
    const { t } = useLingui();
    const create = useCreateJournalEntry();
    const createCounterparty = useCreateCounterparty();
    const toast = useToast();
    const navigate = useNavigate();

    const [form, setForm] = useState<FormState>(() => {
        // Prefill only resolvable postable accounts; lines can't post to placeholders.
        const account = prefillAccountId
            ? accounts.find(a => a.id === prefillAccountId && a.isPostable)
            : undefined;
        return account ? prefilledForm(todayIso(), account) : emptyForm(todayIso());
    });
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<FieldErrors | null>(null);

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

    async function createAndSelectCounterparty(name: string) {
        try {
            const created = await createCounterparty.mutateAsync({ name });
            setHeader({ counterpartyId: created.id });
            toast.success(t`Counterparty '${created.name}' created.`);
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
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
            toast.success(t`Journal entry created.`);
            await navigate({ to: '/journal/$id', params: { id: created.id } });
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
            <Panel>
                {/* Title + back navigation live in the TopBar (breadcrumb +
                 *  "New journal entry"); the lone Cancel action is the button
                 *  beside Create below, so this header is just the description. */}
                <SectionHead
                    subtitle={
                        form.mode === 'simple' ? (
                            <Trans>
                                Money moved from one or more sources to one or more destinations.
                            </Trans>
                        ) : (
                            <Trans>General-journal table - explicit debit / credit per line.</Trans>
                        )
                    }
                />
                <FormErrorBanner message={topError} />
                <HeaderInputs
                    form={form}
                    counterparties={counterparties}
                    onPatch={setHeader}
                    onCreateCounterparty={name => {
                        void createAndSelectCounterparty(name);
                    }}
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
                        onChange={setSimple}
                    />
                ) : (
                    <AdvancedLines
                        advanced={form.advanced}
                        accountsById={accountsById}
                        anchorCurrency={anchorCurrency}
                        catalog={catalog}
                        scaleLookup={scaleLookup}
                        onChange={setAdvanced}
                    />
                )}
                <div className="flex items-center justify-end gap-2 mt-4 pt-3 border-t border-border-soft">
                    <Link
                        to="/activity"
                        search={{ page: 1, q: '', account: '', from: '', to: '' }}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1"
                    >
                        <Trans>Cancel</Trans>
                    </Link>
                    <Button type="submit" variant="primary" isDisabled={create.isPending}>
                        {create.isPending ? (
                            <Trans>Creating…</Trans>
                        ) : (
                            <Trans>Create journal entry</Trans>
                        )}
                    </Button>
                </div>
            </Panel>
        </Form>
    );
}

function HeaderInputs({
    form,
    counterparties,
    onPatch,
    onCreateCounterparty,
}: {
    form: FormState;
    counterparties: { id: CounterpartyId; name: string }[];
    onPatch: (patch: Partial<FormState['header']>) => void;
    onCreateCounterparty: (name: string) => void;
}) {
    const { t } = useLingui();
    const counterpartyItems = useMemo<ComboBoxItem<CounterpartyId>[]>(
        () => counterparties.map(c => ({ key: c.id, label: c.name, value: c.id })),
        [counterparties],
    );

    return (
        <div className="grid grid-cols-[140px_1fr_minmax(220px,300px)] gap-3 mb-4">
            <DatePicker
                label={t`Date`}
                name="Date"
                value={form.header.date}
                onChange={date => {
                    onPatch({ date });
                }}
                isRequired
            />
            <TextField
                label={t`Description`}
                name="Description"
                value={form.header.description}
                onChange={description => {
                    onPatch({ description });
                }}
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
                    value={form.header.counterpartyId}
                    onChange={counterpartyId => {
                        onPatch({ counterpartyId });
                    }}
                    onClear={() => {
                        onPatch({ counterpartyId: null });
                    }}
                    onCreate={onCreateCounterparty}
                    noneLabel={t`── None`}
                    placeholder={t`Pick counterparty…`}
                    ariaLabel={t`Counterparty`}
                />
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
    const { t } = useLingui();
    const segmentClass = (selected: boolean) =>
        cx(
            'px-2 py-1 rounded-lg outline-none cursor-pointer data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
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
                const next = selectedKey(keys);
                if (next === 'simple') onSwitchSimple();
                if (next === 'advanced') onSwitchAdvanced();
            }}
            className="inline-flex items-center gap-1 p-[2px] rounded-lg bg-surface-2 border border-border-soft text-xs font-medium"
        >
            <ToggleButton
                id="simple"
                isDisabled={!canSwitchToSimple}
                aria-label={
                    canSwitchToSimple
                        ? t`Switch to the personal-finance shape`
                        : t`Multi-source-multi-destination journal entries can only be edited in Advanced.`
                }
                className={segmentClass(mode === 'simple')}
            >
                <Trans>Simple</Trans>
            </ToggleButton>
            <ToggleButton id="advanced" className={segmentClass(mode === 'advanced')}>
                <Trans>Advanced</Trans>
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
    onChange,
}: {
    simple: FormState['simple'];
    accountsById: Map<AccountId, Account>;
    anchorCurrency: string | null;
    onChange: (updater: (s: FormState['simple']) => FormState['simple']) => void;
}) {
    const { t } = useLingui();
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
                heading={t`From`}
                subheading={t`Money leaves these accounts (credit).`}
                side="from"
                legs={simple.from}
                anchorCurrency={anchorCurrency}
                accountsById={accountsById}
                onChange={updateLeg}
                onAdd={() => {
                    addLeg('from');
                }}
                onRemove={i => {
                    removeLeg('from', i);
                }}
            />
            <SimpleLegColumn
                heading={t`To`}
                subheading={t`Money arrives in these accounts (debit).`}
                side="to"
                legs={simple.to}
                anchorCurrency={anchorCurrency}
                accountsById={accountsById}
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
    onChange: (side: 'from' | 'to', index: number, patch: Partial<SimpleLeg>) => void;
    onAdd: () => void;
    onRemove: (index: number) => void;
}) {
    const { t } = useLingui();
    return (
        <div className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between">
                <div className="flex flex-col gap-[2px]">
                    <h3 className="text-sm font-semibold text-fg-1">{heading}</h3>
                    <span className="text-xs text-fg-3">{subheading}</span>
                </div>
                <Button variant="ghost" onPress={onAdd} className="px-2 py-1 text-xs font-normal">
                    <Icon name="plus" size={12} strokeWidth={2} />
                    <Trans>Add</Trans>
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
                        <div className="flex-1 min-w-0">
                            <AccountPicker
                                name={`simple.${side}[${i.toString()}].accountId`}
                                value={leg.accountId}
                                filterCurrency={filterCurrency}
                                onChange={accountId => {
                                    onChange(side, i, { accountId });
                                }}
                            />
                        </div>
                        <NumberField
                            aria-label={t`Amount`}
                            name={`simple.${side}[${i.toString()}].amount`}
                            value={leg.amount === '' ? NaN : Number(leg.amount)}
                            onChange={n => {
                                onChange(side, i, { amount: Number.isNaN(n) ? '' : String(n) });
                            }}
                            formatOptions={currencyFormat(
                                legAccount?.currencyCode ?? anchorCurrency,
                            )}
                            placeholder="0.00"
                            inputClassName="text-right font-mono"
                            className="w-[140px] shrink-0"
                        />
                        <IconButton
                            onPress={() => {
                                onRemove(i);
                            }}
                            isDisabled={legs.length === 1}
                            aria-label={t`Remove this leg`}
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
    onChange,
}: {
    advanced: AdvancedLine[];
    accountsById: Map<AccountId, Account>;
    anchorCurrency: string | null;
    catalog: CurrencyCatalog;
    scaleLookup: ScaleLookup;
    onChange: (updater: (a: AdvancedLine[]) => AdvancedLine[]) => void;
}) {
    const { t } = useLingui();
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
            <div className="grid grid-cols-[1fr_120px_120px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
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
                    <Trans>Description</Trans>
                </span>
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
                        <AccountPicker
                            name={`advanced[${i.toString()}].accountId`}
                            value={line.accountId}
                            filterCurrency={filterCurrency}
                            onChange={accountId => {
                                updateLine(i, { accountId });
                            }}
                        />
                        <NumberField
                            aria-label={t`Debit`}
                            name={`advanced[${i.toString()}].debit`}
                            value={line.debit === '' ? NaN : Number(line.debit)}
                            onChange={n => {
                                updateLine(i, { debit: Number.isNaN(n) ? '' : String(n) });
                            }}
                            formatOptions={currencyFormat(
                                lineAccount?.currencyCode ?? anchorCurrency,
                            )}
                            placeholder="0.00"
                            inputClassName="text-right font-mono"
                        />
                        <NumberField
                            aria-label={t`Credit`}
                            name={`advanced[${i.toString()}].credit`}
                            value={line.credit === '' ? NaN : Number(line.credit)}
                            onChange={n => {
                                updateLine(i, { credit: Number.isNaN(n) ? '' : String(n) });
                            }}
                            formatOptions={currencyFormat(
                                lineAccount?.currencyCode ?? anchorCurrency,
                            )}
                            placeholder="0.00"
                            inputClassName="text-right font-mono"
                        />
                        <TextField
                            aria-label={t`Line description`}
                            value={line.description}
                            onChange={description => {
                                updateLine(i, { description });
                            }}
                            maxLength={500}
                            placeholder={t`Optional`}
                        />
                        <IconButton
                            onPress={() => {
                                removeLine(i);
                            }}
                            isDisabled={advanced.length <= 2}
                            aria-label={t`Remove this line`}
                            className="self-start mt-[2px] data-[hovered]:text-danger data-[hovered]:bg-transparent"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                        </IconButton>
                    </div>
                );
            })}
            <div className="flex items-center justify-between mt-2">
                <Button variant="ghost" onPress={addLine} className="px-2 py-1 text-xs font-normal">
                    <Icon name="plus" size={12} strokeWidth={2} />
                    <Trans>Add line</Trans>
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
        return (
            <span className="text-xs text-fg-3">
                <Trans>Σ Debit / Credit</Trans>
            </span>
        );
    }
    const debitStr = formatMoney(totals.debitMinor, currency, catalog);
    const creditStr = formatMoney(totals.creditMinor, currency, catalog);
    const diff = totals.debitMinor - totals.creditMinor;
    return (
        <div className="flex items-center gap-4 text-xs tabular-nums">
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
                    <Trans>Off by {formatMoney(Math.abs(diff), currency, catalog)}</Trans>
                </span>
            )}
        </div>
    );
}

function AccountPicker({
    name,
    value,
    filterCurrency,
    onChange,
}: {
    name: string;
    value: AccountId | null;
    filterCurrency: string | null;
    onChange: (accountId: AccountId | null) => void;
}) {
    const { t } = useLingui();
    // Journal lines post to leaves only; the currency is anchored to the first
    // chosen leg so every line in the entry shares one currency.
    return (
        <AccountSelect
            name={name}
            value={value}
            onChange={onChange}
            postableOnly
            currencyCode={filterCurrency ?? undefined}
            placeholder={t`Select…`}
            ariaLabel={t`Account`}
        />
    );
}
