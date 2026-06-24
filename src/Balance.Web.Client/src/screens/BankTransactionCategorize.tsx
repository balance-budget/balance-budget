import { useEffect, useMemo, useRef, useState } from 'react';
import { Form } from 'react-aria-components';
import { Link, useNavigate } from '@tanstack/react-router';
import { Trans, useLingui } from '@lingui/react/macro';
import { useAccounts, type Account } from '../api/accounts';
import { useBankAccounts, type BankAccount } from '../api/bankAccounts';
import {
    useAttachBankTransaction,
    useAttachCandidates,
    useBankTransaction,
    useCategorizeBankTransaction,
    type BankTransaction,
    type BankTransactionDetail,
} from '../api/bankTransactions';
import {
    useCounterparties,
    useSuggestedCounterAccounts,
    type Counterparty,
    type SuggestedCounterAccount,
} from '../api/counterparties';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useLoanPaymentProposal, type LoanProposal } from '../api/loans';
import { AccountSelect } from '../components/AccountSelect';
import { BankTransactionDetails } from '../components/BankTransactionDetails';
import { Checkbox } from '../components/ui/Checkbox';
import { ComboBox } from '../components/ui/ComboBox';
import { type ComboBoxItem } from '../components/ui/combobox.state';
import { ErrorState } from '../components/ErrorState';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { IconButton } from '../components/ui/Button';
import { DatePicker } from '../components/ui/DatePicker';
import { NumberField } from '../components/ui/NumberField';
import { SearchField } from '../components/ui/SearchField';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { todayIso } from '../lib/dates';
import {
    type AccountId,
    type BankAccountId,
    type BankTransactionId,
    type CounterpartyId,
    type JournalEntryId,
    type LoanId,
} from '../lib/domain';
import { handleFormError } from '../lib/formErrors';
import { formatMoney } from '../lib/money';
import {
    applySuggestionsToLines,
    buildRequest,
    computeTotals,
    emptyLine,
    formatMagnitudeFor,
    initialForm,
    linesFromLoanProposal,
    resolveOpenContext,
    type CategorizeFormState,
    type FieldErrors,
    type LineInput,
} from './bankTransactionCategorize.state';

type Props = { id: BankTransactionId; loanId: LoanId | null };

export function BankTransactionCategorize({ id, loanId }: Props) {
    const { t } = useLingui();
    const bt = useBankTransaction(id);
    const accounts = useAccounts();
    const counterparties = useCounterparties();
    const bankAccounts = useBankAccounts();
    const catalog = useCurrencyCatalog();

    if (bt.isPending || accounts.isPending || counterparties.isPending || bankAccounts.isPending) {
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
                    message={t`Couldn't load bank transaction.`}
                    onRetry={() => void bt.refetch()}
                />
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

    if (bankAccounts.isError) {
        return (
            <Panel>
                <ErrorState
                    message={t`Couldn't load bank accounts.`}
                    onRetry={() => void bankAccounts.refetch()}
                />
            </Panel>
        );
    }

    if (bt.data.journalEntryId !== null || bt.data.dismissedAt !== null) {
        return <NotCategorizableState bt={bt.data} />;
    }

    return (
        <CategorizeForm
            bt={bt.data}
            loanId={loanId}
            accounts={accounts.data}
            counterparties={counterparties.data}
            bankAccounts={bankAccounts.data}
            catalog={catalog}
        />
    );
}

function NotCategorizableState({ bt }: { bt: BankTransaction }) {
    const { t } = useLingui();
    const reason = bt.journalEntryId
        ? t`This row already has a journal entry.`
        : t`This row is dismissed. Undismiss it first to categorize.`;
    return (
        <Panel>
            <SectionHead
                title={t`Categorize bank transaction`}
                action={
                    <Link
                        to="/bank-transactions"
                        search={{ page: 1, filter: 'Inbox', q: '' }}
                        className="text-xs text-fg-3 hover:text-fg-1"
                    >
                        ← <Trans>Back to inbox</Trans>
                    </Link>
                }
            />
            <div className="py-6 flex flex-col items-center gap-2 text-center">
                <span className="text-sm text-fg-2">{reason}</span>
            </div>
        </Panel>
    );
}

function CategorizeForm({
    bt,
    loanId,
    accounts,
    counterparties,
    bankAccounts,
    catalog,
}: {
    bt: BankTransactionDetail;
    loanId: LoanId | null;
    accounts: Account[];
    counterparties: Counterparty[];
    bankAccounts: BankAccount[];
    catalog: CurrencyCatalog;
}) {
    const { t } = useLingui();
    const categorize = useCategorizeBankTransaction();
    const toast = useToast();
    const navigate = useNavigate();

    const openContext = useMemo(
        () => resolveOpenContext(bt.counterpartyAccountNumber, bankAccounts),
        [bt.counterpartyAccountNumber, bankAccounts],
    );
    const resolvedCounterpartyId =
        openContext.kind === 'counterparty' ? openContext.counterpartyId : null;
    const prefilledAccountId =
        openContext.kind === 'self-transfer' ? openContext.prefilledAccountId : null;

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
            prefilledAccountId,
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

    // Loan mode (ADR-0025): pre-fill the engine's per-part interest/principal
    // proposal for the BT's month. The user confirms rather than calculates;
    // every amount stays editable because the bank's actual charge wins.
    const proposal = useLoanPaymentProposal(loanId, bt.bookingDate);
    const [includedParts, setIncludedParts] = useState<ReadonlySet<string> | null>(null);

    const activeCounterpartyId = form.counterpartyMode === 'existing' ? form.counterpartyId : null;
    const suggestions = useSuggestedCounterAccounts(activeCounterpartyId);

    // Track whether the user has interacted with the lines so we don't clobber
    // their edits when suggestions land asynchronously. Pre-fill happens once
    // per counterparty change while the form is still pristine.
    const pristine = useRef(true);
    const lastAppliedCounterpartyId = useRef<CounterpartyId | null>(null);

    // Loan-mode pre-fill: (re-)apply whenever the proposal lands or the part
    // subset changes; manual edits stop the re-fill until the subset changes
    // again (changing the subset is an explicit "start over from the engine").
    const lastAppliedLoanKey = useRef<string | null>(null);
    useEffect(() => {
        const data = proposal.data;
        if (loanId === null || !data) return;
        const key = `${loanId}|${includedParts === null ? '*' : [...includedParts].sort().join(',')}`;
        if (lastAppliedLoanKey.current === key) return;
        if (lastAppliedLoanKey.current !== null && !pristine.current) {
            // subset change overrides edits deliberately
        } else if (!pristine.current) {
            return;
        }
        setForm(prev => ({
            ...prev,
            lines: linesFromLoanProposal(data, includedParts, formatMagnitude),
        }));
        pristine.current = true;
        lastAppliedLoanKey.current = key;
    }, [loanId, proposal.data, includedParts, formatMagnitude]);

    useEffect(() => {
        if (loanId !== null) return; // loan mode owns the pre-fill
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
        loanId,
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
            toast.success(t`Categorized.`);
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
                <SectionHead
                    title={t`Categorize bank transaction`}
                    subtitle={t`Turn this bank row into a journal entry.`}
                    action={
                        <Link
                            to="/bank-transactions"
                            search={{ page: 1, filter: 'Inbox', q: '' }}
                            className="text-xs text-fg-3 hover:text-fg-1"
                        >
                            ← <Trans>Cancel</Trans>
                        </Link>
                    }
                />
                <div className="mb-4">
                    <BankTransactionDetails bt={bt} catalog={catalog} />
                </div>
                {loanId === null ? (
                    <AttachOptionsPanel bt={bt} catalog={catalog} />
                ) : (
                    <LoanModePanel
                        bt={bt}
                        proposal={proposal.data ?? null}
                        includedParts={includedParts}
                        onToggle={partId => {
                            setIncludedParts(prev => {
                                const all = new Set(
                                    (proposal.data?.lines ?? []).map(l => l.loanPartId as string),
                                );
                                const next = new Set(prev ?? all);
                                if (next.has(partId)) next.delete(partId);
                                else next.add(partId);
                                return next;
                            });
                        }}
                    />
                )}
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
                        search={{ page: 1, filter: 'Inbox', q: '' }}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1"
                    >
                        <Trans>Cancel</Trans>
                    </Link>
                    <button
                        type="submit"
                        disabled={categorize.isPending}
                        className="px-3 py-[7px] rounded-lg text-sm font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {categorize.isPending ? t`Categorizing…` : t`Categorize`}
                    </button>
                </div>
            </Panel>
        </Form>
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
    const { t } = useLingui();
    return (
        <div className="grid grid-cols-1 lg:grid-cols-[140px_1fr_minmax(220px,300px)] gap-3">
            <DatePicker
                label={t`Date`}
                name="Date"
                value={form.date}
                onChange={date => {
                    onPatch({ date });
                }}
                isRequired
            />
            <TextField
                label={t`Description`}
                name="Description"
                value={form.description}
                onChange={description => {
                    onPatch({ description });
                }}
                maxLength={500}
                placeholder={t`Optional`}
            />
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
    const { t } = useLingui();
    const items = useMemo<ComboBoxItem<CounterpartyId | null>[]>(
        () =>
            [...counterparties]
                .sort((a, b) => a.name.localeCompare(b.name))
                .map(c => ({ key: c.id, label: c.name, value: c.id })),
        [counterparties],
    );

    const effectiveItems = useMemo(() => {
        if (form.counterpartyMode !== 'new' || form.newCounterpartyName.trim().length === 0) {
            return items;
        }
        const pending: ComboBoxItem<CounterpartyId | null> = {
            key: '__pending__',
            label: t`${form.newCounterpartyName.trim()} (new)`,
            value: null,
        };
        return [pending, ...items];
    }, [form.counterpartyMode, form.newCounterpartyName, items, t]);

    const value: CounterpartyId | null =
        form.counterpartyMode === 'existing' ? form.counterpartyId : null;

    return (
        <div className="flex flex-col gap-1">
            <span className="text-xs font-medium text-fg-2">
                <Trans>Counterparty</Trans>
            </span>
            <ComboBox
                items={effectiveItems}
                value={value}
                onChange={id => {
                    onPatch({
                        counterpartyMode: 'existing',
                        counterpartyId: id,
                        newCounterpartyName: '',
                    });
                }}
                onClear={() => {
                    onPatch({
                        counterpartyMode: 'existing',
                        counterpartyId: null,
                        newCounterpartyName: '',
                    });
                }}
                onCreate={typed => {
                    onPatch({
                        counterpartyMode: 'new',
                        counterpartyId: null,
                        newCounterpartyName: typed,
                    });
                }}
                noneLabel={t`── None (self-transfer)`}
                placeholder={t`Pick counterparty…`}
                ariaLabel={t`Counterparty`}
            />
            <FieldError
                name={
                    form.counterpartyMode === 'existing' ? 'CounterpartyId' : 'NewCounterparty.Name'
                }
                errors={fieldErrors}
            />
        </div>
    );
}

function Lines({
    lines,
    bankAccounts,
    bankTransactionBankAccountId,
    currencyCode,
    fieldErrors,
    onUpdate,
    onAdd,
    onRemove,
}: {
    lines: LineInput[];
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
    // (Current → Savings, Current → Credit Card) work — see ADR 0013(e).
    const ownBankSideAccountId = useMemo(() => {
        const ba = bankAccounts.find(b => b.id === bankTransactionBankAccountId);
        return ba?.accountId ?? null;
    }, [bankAccounts, bankTransactionBankAccountId]);

    return (
        <div className="flex flex-col">
            <div className="hidden lg:grid grid-cols-[1fr_140px_minmax(140px,1fr)_32px] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>
                    <Trans>Account</Trans>
                </span>
                <span className="text-right">
                    <Trans>Amount</Trans>
                </span>
                <span>
                    <Trans>Description</Trans>
                </span>
                <span />
            </div>
            {lines.map((line, i) => (
                <LineRow
                    key={line.id}
                    line={line}
                    index={i}
                    canRemove={lines.length > 1}
                    currencyCode={currencyCode}
                    excludeAccountId={ownBankSideAccountId}
                    onUpdate={onUpdate}
                    onRemove={onRemove}
                />
            ))}
            <FieldError name="Lines" errors={fieldErrors} />
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

function LineRow({
    line,
    index,
    canRemove,
    currencyCode,
    excludeAccountId,
    onUpdate,
    onRemove,
}: {
    line: LineInput;
    index: number;
    canRemove: boolean;
    currencyCode: string;
    excludeAccountId: AccountId | null;
    onUpdate: (index: number, patch: Partial<LineInput>) => void;
    onRemove: (index: number) => void;
}) {
    const { t } = useLingui();
    return (
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_140px_minmax(140px,1fr)_32px] gap-3 items-start px-2 py-2 border-b border-border-soft last:border-b-0">
            <AccountSelect
                name={`lines[${index.toString()}].accountId`}
                value={line.accountId}
                onChange={accountId => {
                    onUpdate(index, { accountId });
                }}
                postableOnly
                currencyCode={currencyCode}
                exclude={excludeAccountId ? [excludeAccountId] : undefined}
                placeholder={t`Pick account…`}
                ariaLabel={t`Account`}
            />
            <NumberField
                aria-label={t`Amount`}
                name={`lines[${index.toString()}].amount`}
                value={line.amount === '' ? NaN : Number(line.amount)}
                onChange={n => {
                    onUpdate(index, { amount: Number.isNaN(n) ? '' : String(n) });
                }}
                formatOptions={{ style: 'currency', currency: currencyCode }}
                placeholder="0.00"
                inputClassName="text-right font-mono"
            />
            <TextField
                aria-label={t`Line description`}
                value={line.description}
                onChange={description => {
                    onUpdate(index, { description });
                }}
                maxLength={500}
                placeholder={t`Optional`}
                inputClassName="text-sm"
            />
            <IconButton
                onPress={() => {
                    onRemove(index);
                }}
                isDisabled={!canRemove}
                aria-label={t`Remove this line`}
                className="self-end lg:self-start mt-[6px] data-[hovered]:text-danger data-[hovered]:bg-transparent"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </IconButton>
        </div>
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
        <div className="flex items-center justify-end gap-4 mt-3 text-xs tabular-nums">
            <span className="text-fg-3">
                <Trans>Target</Trans> <span className="font-mono text-fg-1">{targetStr}</span>
            </span>
            <span className="text-fg-3">
                <Trans>Allocated</Trans> <span className="font-mono text-fg-1">{allocatedStr}</span>
            </span>
            {totals.balanced ? (
                <span className="inline-flex items-center gap-1 text-success">
                    <Icon name="check-circle" size={12} strokeWidth={2} /> <Trans>Balanced</Trans>
                </span>
            ) : (
                <span className="inline-flex items-center gap-1 text-danger">
                    <Icon name="alert-circle" size={12} strokeWidth={2} />
                    {totals.unallocatedMinor > 0 ? (
                        <Trans>Unallocated</Trans>
                    ) : (
                        <Trans>Over by</Trans>
                    )}{' '}
                    {unallocatedStr}
                </span>
            )}
        </div>
    );
}

/**
 * Loan-aware mode header (ADR-0025): names the loan, lets the user scope the
 * payment to a subset of parts (banks that debit parts separately), and offers
 * the way back to plain categorization. The engine's proposal is applied to
 * the editable lines below.
 */
function LoanModePanel({
    bt,
    proposal,
    includedParts,
    onToggle,
}: {
    bt: BankTransactionDetail;
    proposal: LoanProposal | null;
    includedParts: ReadonlySet<string> | null;
    onToggle: (partId: string) => void;
}) {
    return (
        <div className="mb-4 px-3 py-2 rounded-lg bg-surface-2 border border-border-soft text-xs">
            <div className="flex flex-wrap items-center gap-2">
                <span className="inline-flex items-center gap-1 text-brand-primary font-medium">
                    <Icon name="landmark" size={13} strokeWidth={2} />
                    <Trans>Loan payment</Trans>
                </span>
                {proposal === null ? (
                    <span className="text-fg-3">
                        <Trans>Computing the proposal…</Trans>
                    </span>
                ) : (
                    <span className="text-fg-3">
                        <Trans>
                            Pre-filled from the schedule for {proposal.month.slice(0, 7)} - adjust
                            the amounts to match the bank&apos;s charge.
                        </Trans>
                    </span>
                )}
                <Link
                    to="/bank-transactions/$id/categorize"
                    params={{ id: bt.id }}
                    search={{}}
                    className="ml-auto text-fg-3 hover:text-fg-1"
                >
                    <Trans>Categorize normally</Trans>
                </Link>
            </div>
            {proposal !== null && proposal.lines.length > 1 && (
                <div className="flex flex-wrap items-center gap-3 mt-2 pt-2 border-t border-border-soft">
                    <span className="text-fg-3">
                        <Trans>Parts in this payment:</Trans>
                    </span>
                    {proposal.lines.map(line => {
                        const included =
                            includedParts === null || includedParts.has(line.loanPartId);
                        return (
                            <Checkbox
                                key={line.loanPartId}
                                isSelected={included}
                                onChange={() => {
                                    onToggle(line.loanPartId);
                                }}
                            >
                                {line.label}
                            </Checkbox>
                        );
                    })}
                </div>
            )}
        </div>
    );
}

function filterSuggestionsByCurrency(
    suggestions: readonly SuggestedCounterAccount[],
    accountsById: Map<AccountId, Account>,
    currencyCode: string,
): SuggestedCounterAccount[] {
    return suggestions.filter(s => accountsById.get(s.accountId)?.currencyCode === currencyCode);
}

// ─────────────────────────────────────────────────────────────────────────────
// Attach options: three choices for a sibling-shaped BT (ADR 0013).
//  1. Quick attach — only when the strict predicate hit a unique JE (the hint).
//  2. Pick a JE — manual JE-picker over structural candidates with a widened
//     date window the user controls.
//  3. Create a new JE — the existing categorize form below this panel.
// ─────────────────────────────────────────────────────────────────────────────

function AttachOptionsPanel({
    bt,
    catalog,
}: {
    bt: BankTransactionDetail;
    catalog: CurrencyCatalog;
}) {
    const { t } = useLingui();
    const [pickerOpen, setPickerOpen] = useState(false);
    const attach = useAttachBankTransaction();
    const toast = useToast();
    const navigate = useNavigate();
    const hint = bt.matchingJournalEntry;

    async function attachTo(journalEntryId: JournalEntryId) {
        try {
            const detail = await attach.mutateAsync({ id: bt.id, journalEntryId });
            toast.success(t`Attached.`);
            await navigate({ to: '/journal/$id', params: { id: detail.id } });
        } catch (err) {
            if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <div className="mb-4 px-3 py-2 rounded-lg bg-surface-2 border border-border-soft text-xs">
            <div className="flex flex-wrap items-center gap-2">
                <span className="text-fg-3">
                    <Trans>Sibling-of-self-transfer?</Trans>
                </span>
                {hint && (
                    <button
                        type="button"
                        onClick={() => void attachTo(hint.id)}
                        disabled={attach.isPending}
                        className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-brand-primary hover:bg-brand-primary-soft disabled:opacity-60"
                    >
                        <Icon name="link" size={13} strokeWidth={2} />
                        <Trans>
                            Attach to journal entry on {hint.date} · {hint.otherAccountName}
                        </Trans>
                    </button>
                )}
                <button
                    type="button"
                    onClick={() => {
                        setPickerOpen(true);
                    }}
                    disabled={attach.isPending}
                    className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-fg-1 border border-border-strong hover:bg-surface-1 disabled:opacity-60"
                >
                    <Icon name="search" size={13} strokeWidth={2} />
                    <Trans>Pick a journal entry to attach to…</Trans>
                </button>
                <span className="text-fg-3">
                    <Trans>or scroll down to create a new journal entry.</Trans>
                </span>
            </div>

            {pickerOpen && (
                <JePickerModal
                    bt={bt}
                    catalog={catalog}
                    onClose={() => {
                        setPickerOpen(false);
                    }}
                    onPick={id => {
                        setPickerOpen(false);
                        void attachTo(id);
                    }}
                />
            )}
        </div>
    );
}

function JePickerModal({
    bt,
    catalog,
    onClose,
    onPick,
}: {
    bt: BankTransactionDetail;
    catalog: CurrencyCatalog;
    onClose: () => void;
    onPick: (id: JournalEntryId) => void;
}) {
    const { t } = useLingui();
    const [days, setDays] = useState(14);
    const candidates = useAttachCandidates(bt.id, days);
    const [query, setQuery] = useState('');

    const filtered = useMemo(() => {
        const data = candidates.data ?? [];
        const q = query.trim().toLowerCase();
        if (q.length === 0) return data;
        return data.filter(
            c =>
                (c.description ?? '').toLowerCase().includes(q) ||
                c.otherAccountName.toLowerCase().includes(q) ||
                c.date.includes(q),
        );
    }, [candidates.data, query]);

    return (
        <Modal
            open
            onClose={onClose}
            title={t`Pick a journal entry to attach to`}
            description={t`Structural matches with an Uncleared bank-side slot. Widen the date window to surface near-misses.`}
            width="md"
        >
            <div className="flex flex-wrap items-center gap-3 mb-3">
                <NumberField
                    label={t`Date window (±days)`}
                    minValue={0}
                    maxValue={365}
                    value={days}
                    onChange={value => {
                        setDays(Number.isNaN(value) ? 0 : Math.max(0, Math.min(365, value)));
                    }}
                    className="flex-row items-center gap-2"
                    inputClassName="w-20"
                />
                <SearchField
                    aria-label={t`Filter candidates`}
                    value={query}
                    onChange={setQuery}
                    placeholder={t`Filter…`}
                    className="flex-1 min-w-[160px]"
                />
            </div>

            {candidates.isPending && <Skeleton className="h-6 w-full mb-2" />}
            {candidates.isError && (
                <ErrorState
                    message={t`Couldn't load candidates.`}
                    onRetry={() => void candidates.refetch()}
                />
            )}
            {candidates.data && filtered.length === 0 && (
                <p className="text-xs text-fg-3">
                    <Trans>
                        No structural matches in this window. Widen the date range or fall back to
                        creating a new JE below.
                    </Trans>
                </p>
            )}

            {filtered.length > 0 && (
                <ul className="flex flex-col gap-1 max-h-80 overflow-auto">
                    {filtered.map(candidate => (
                        <li key={candidate.id}>
                            <button
                                type="button"
                                onClick={() => {
                                    onPick(candidate.id);
                                }}
                                className="w-full text-left px-3 py-2 rounded-lg border border-border-soft hover:bg-surface-2 flex items-baseline justify-between gap-2"
                            >
                                <span className="flex flex-col leading-tight min-w-0">
                                    <span className="text-sm text-fg-1 truncate">
                                        {candidate.description ?? t`(no description)`}
                                    </span>
                                    <span className="text-xs text-fg-3 truncate">
                                        {candidate.date} · {candidate.otherAccountName}
                                    </span>
                                </span>
                                <span className="text-xs font-mono text-fg-2 tabular-nums shrink-0">
                                    {formatMoney(candidate.amount, bt.money.currencyCode, catalog, {
                                        sign: true,
                                    })}
                                </span>
                            </button>
                        </li>
                    ))}
                </ul>
            )}

            <ModalFooter>
                <button
                    type="button"
                    onClick={onClose}
                    className="px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1"
                >
                    <Trans>Cancel</Trans>
                </button>
            </ModalFooter>
        </Modal>
    );
}
