import { useState } from 'react';
import {
    useBankAccountImporters,
    useCreateBankAccount,
    useUpdateBankAccount,
    type BankAccount,
    type BankAccountType,
} from '../api/bankAccounts';
import { useAccounts } from '../api/accounts';
import { useCounterparties } from '../api/counterparties';
import { useCurrencies } from '../api/currencies';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Modal, ModalFooter } from '../components/Modal';
import { useToast } from '../components/Toast';
import { isLedgerAccount } from '../lib/domain';
import type { AccountId, CounterpartyId } from '../lib/domain';
import { ApiError } from '../lib/http';

export type BankAccountOwnerPrefill = { accountId: AccountId } | { counterpartyId: CounterpartyId };

type OwnerKind = 'account' | 'counterparty';

type Props = { onClose: () => void } & (
    | { mode: 'create'; ownerPrefill?: BankAccountOwnerPrefill }
    | { mode: 'edit'; bankAccount: BankAccount }
);

type FormState = {
    type: BankAccountType;
    iban: string;
    accountNumber: string;
    cardIdentifier: string;
    bic: string;
    bankName: string;
    accountHolderName: string;
    currencyCode: string;
    importerKey: string;
    ownerKind: OwnerKind;
    accountId: string;
    counterpartyId: string;
};

function initialState(props: Props): FormState {
    if (props.mode === 'edit') {
        const ba = props.bankAccount;
        const ownerKind: OwnerKind = ba.accountId !== null ? 'account' : 'counterparty';
        return {
            type: ba.type,
            iban: ba.iban ?? '',
            accountNumber: ba.accountNumber ?? '',
            cardIdentifier: ba.cardIdentifier ?? '',
            bic: ba.bic ?? '',
            bankName: ba.bankName ?? '',
            accountHolderName: ba.accountHolderName ?? '',
            currencyCode: ba.currencyCode ?? '',
            importerKey: ba.importerKey ?? '',
            ownerKind,
            accountId: ba.accountId ?? '',
            counterpartyId: ba.counterpartyId ?? '',
        };
    }
    const pre = props.ownerPrefill;
    const ownerKind: OwnerKind = pre && 'accountId' in pre ? 'account' : 'counterparty';
    return {
        type: 'Current',
        iban: '',
        accountNumber: '',
        cardIdentifier: '',
        bic: '',
        bankName: '',
        accountHolderName: '',
        currencyCode: '',
        importerKey: '',
        ownerKind,
        accountId: pre && 'accountId' in pre ? pre.accountId : '',
        counterpartyId: pre && 'counterpartyId' in pre ? pre.counterpartyId : '',
    };
}

export function BankAccountFormModal(props: Props) {
    const create = useCreateBankAccount();
    const update = useUpdateBankAccount();
    const toast = useToast();
    const accounts = useAccounts();
    const counterparties = useCounterparties();
    const currencies = useCurrencies();
    const importers = useBankAccountImporters();

    const [form, setForm] = useState<FormState>(() => initialState(props));
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const ownerLocked = props.mode === 'create' && props.ownerPrefill !== undefined;
    const isPending = create.isPending || update.isPending;

    function update_(patch: Partial<FormState>) {
        setForm(prev => ({ ...prev, ...patch }));
    }

    function changeType(next: BankAccountType) {
        // Card BankAccounts are owned-only (ADR 0019). Snap ownerKind back to 'account'
        // and clear any incompatible ImporterKey when the user picks Card.
        const nextOwner: OwnerKind = next === 'Card' ? 'account' : form.ownerKind;
        const currentImporter = importers.data?.find(i => i.key === form.importerKey);
        const nextImporter = currentImporter?.supportedType === next ? form.importerKey : '';
        update_({ type: next, ownerKind: nextOwner, importerKey: nextImporter });
    }

    async function submit() {
        setTopError(null);
        setFieldErrors(null);

        const currencyRequired = form.ownerKind === 'account';
        const trimmedCurrency = form.currencyCode.trim();
        const payloadCurrency =
            trimmedCurrency.length === 0 ? (currencyRequired ? '' : null) : trimmedCurrency;

        const accountIdValue = form.ownerKind === 'account' ? (form.accountId as AccountId) : null;
        const counterpartyIdValue =
            form.ownerKind === 'counterparty' ? (form.counterpartyId as CounterpartyId) : null;

        try {
            if (props.mode === 'create') {
                await create.mutateAsync({
                    type: form.type,
                    iban: emptyToNull(form.iban),
                    accountNumber: emptyToNull(form.accountNumber),
                    cardIdentifier: emptyToNull(form.cardIdentifier),
                    bic: emptyToNull(form.bic),
                    bankName: emptyToNull(form.bankName),
                    accountHolderName: emptyToNull(form.accountHolderName),
                    currencyCode: payloadCurrency,
                    importerKey: emptyToNull(form.importerKey),
                    accountId: accountIdValue,
                    counterpartyId: counterpartyIdValue,
                });
            } else {
                const original = bankAccountToUpdateInput(props.bankAccount);
                const edited = {
                    type: form.type,
                    iban: emptyToNull(form.iban),
                    accountNumber: emptyToNull(form.accountNumber),
                    cardIdentifier: emptyToNull(form.cardIdentifier),
                    bic: emptyToNull(form.bic),
                    bankName: emptyToNull(form.bankName),
                    accountHolderName: emptyToNull(form.accountHolderName),
                    currencyCode: payloadCurrency,
                    importerKey: emptyToNull(form.importerKey),
                    accountId: accountIdValue,
                    counterpartyId: counterpartyIdValue,
                };
                await update.mutateAsync({ id: props.bankAccount.id, original, edited });
            }
            props.onClose();
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

    const ledgerAccounts = (accounts.data ?? []).filter(isLedgerAccount);
    const counterpartyList = counterparties.data ?? [];
    const currencyList = Array.from(currencies.data?.values() ?? []);
    const importerOptions = (importers.data ?? []).filter(i => i.supportedType === form.type);
    const ownerKindLocked = ownerLocked || form.type === 'Card';

    return (
        <Modal
            open
            onClose={props.onClose}
            title={props.mode === 'create' ? 'New bank account' : 'Edit bank account'}
            width="md"
        >
            <form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                noValidate
            >
                <FormErrorBanner message={topError} />

                <fieldset className="mb-4">
                    <legend className="text-[12px] font-medium text-fg-2 mb-2">Type</legend>
                    <div className="flex gap-4">
                        <RadioOption
                            label="Current"
                            checked={form.type === 'Current'}
                            onChange={() => {
                                changeType('Current');
                            }}
                        />
                        <RadioOption
                            label="Savings"
                            checked={form.type === 'Savings'}
                            onChange={() => {
                                changeType('Savings');
                            }}
                        />
                        <RadioOption
                            label="Card"
                            checked={form.type === 'Card'}
                            onChange={() => {
                                changeType('Card');
                            }}
                        />
                    </div>
                    <FieldError name="Type" errors={fieldErrors} />
                </fieldset>

                <fieldset className="mb-4">
                    <legend className="text-[12px] font-medium text-fg-2 mb-2">Owner</legend>
                    <div className="flex gap-4 mb-2">
                        <RadioOption
                            label="Account"
                            checked={form.ownerKind === 'account'}
                            disabled={ownerKindLocked}
                            onChange={() => {
                                update_({ ownerKind: 'account' });
                            }}
                        />
                        <RadioOption
                            label="Counterparty"
                            checked={form.ownerKind === 'counterparty'}
                            disabled={ownerKindLocked}
                            onChange={() => {
                                update_({ ownerKind: 'counterparty' });
                            }}
                        />
                    </div>
                    {form.ownerKind === 'account' ? (
                        <select
                            value={form.accountId}
                            onChange={e => {
                                update_({ accountId: e.target.value });
                            }}
                            disabled={ownerLocked}
                            required
                            className="w-full px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong disabled:opacity-60"
                        >
                            <option value="">Select an account…</option>
                            {ledgerAccounts.map(a => (
                                <option key={a.id} value={a.id}>
                                    {a.name} ({a.type})
                                </option>
                            ))}
                        </select>
                    ) : (
                        <select
                            value={form.counterpartyId}
                            onChange={e => {
                                update_({ counterpartyId: e.target.value });
                            }}
                            disabled={ownerLocked}
                            required
                            className="w-full px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong disabled:opacity-60"
                        >
                            <option value="">Select a counterparty…</option>
                            {counterpartyList.map(c => (
                                <option key={c.id} value={c.id}>
                                    {c.name}
                                </option>
                            ))}
                        </select>
                    )}
                    <FieldError name="AccountId" errors={fieldErrors} />
                    <FieldError name="CounterpartyId" errors={fieldErrors} />
                </fieldset>

                <div className="grid grid-cols-2 gap-3">
                    {form.type !== 'Card' ? (
                        <TextField
                            label={`IBAN${form.type === 'Current' ? ' *' : ''}`}
                            value={form.iban}
                            onChange={v => {
                                update_({ iban: v });
                            }}
                            errorName="Iban"
                            errors={fieldErrors}
                            autoFocus
                        />
                    ) : null}
                    {form.type === 'Savings' ? (
                        <TextField
                            label="Account number"
                            value={form.accountNumber}
                            onChange={v => {
                                update_({ accountNumber: v });
                            }}
                            errorName="AccountNumber"
                            errors={fieldErrors}
                        />
                    ) : null}
                    {form.type === 'Card' ? (
                        <TextField
                            label="Card identifier *"
                            value={form.cardIdentifier}
                            onChange={v => {
                                update_({ cardIdentifier: v });
                            }}
                            errorName="CardIdentifier"
                            errors={fieldErrors}
                            autoFocus
                        />
                    ) : null}
                    <TextField
                        label="BIC"
                        value={form.bic}
                        onChange={v => {
                            update_({ bic: v });
                        }}
                        errorName="Bic"
                        errors={fieldErrors}
                    />
                    <TextField
                        label="Bank name"
                        value={form.bankName}
                        onChange={v => {
                            update_({ bankName: v });
                        }}
                        errorName="BankName"
                        errors={fieldErrors}
                    />
                    <TextField
                        label="Account holder name"
                        value={form.accountHolderName}
                        onChange={v => {
                            update_({ accountHolderName: v });
                        }}
                        errorName="AccountHolderName"
                        errors={fieldErrors}
                    />
                    <label className="flex flex-col gap-1">
                        <span className="text-[12px] font-medium text-fg-2">
                            Currency
                            {form.ownerKind === 'account' ? (
                                <span className="text-danger"> *</span>
                            ) : null}
                        </span>
                        <select
                            value={form.currencyCode}
                            onChange={e => {
                                update_({ currencyCode: e.target.value });
                            }}
                            required={form.ownerKind === 'account'}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        >
                            <option value="">
                                {form.ownerKind === 'account' ? 'Select…' : '(none)'}
                            </option>
                            {currencyList.map(c => (
                                <option key={c.code} value={c.code}>
                                    {c.code} — {c.name}
                                </option>
                            ))}
                        </select>
                        <FieldError name="CurrencyCode" errors={fieldErrors} />
                    </label>
                    {form.ownerKind === 'account' ? (
                        <label className="flex flex-col gap-1">
                            <span className="text-[12px] font-medium text-fg-2">
                                Statement importer
                            </span>
                            <select
                                value={form.importerKey}
                                onChange={e => {
                                    update_({ importerKey: e.target.value });
                                }}
                                className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                            >
                                <option value="">(none)</option>
                                {importerOptions.map(i => (
                                    <option key={i.key} value={i.key}>
                                        {i.key}
                                    </option>
                                ))}
                            </select>
                            <FieldError name="ImporterKey" errors={fieldErrors} />
                        </label>
                    ) : null}
                </div>

                <p className="mt-3 text-[12px] text-fg-3">
                    {form.type === 'Current'
                        ? 'IBAN is required.'
                        : form.type === 'Savings'
                          ? 'IBAN or Account number is required.'
                          : 'Card identifier is required. Card accounts are owned-only.'}
                </p>

                <ModalFooter>
                    <button
                        type="button"
                        onClick={props.onClose}
                        disabled={isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={isPending}
                        className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {isPending ? 'Saving…' : props.mode === 'create' ? 'Create' : 'Save'}
                    </button>
                </ModalFooter>
            </form>
        </Modal>
    );
}

function emptyToNull(v: string): string | null {
    const trimmed = v.trim();
    return trimmed.length === 0 ? null : trimmed;
}

function bankAccountToUpdateInput(ba: BankAccount) {
    return {
        type: ba.type,
        iban: ba.iban,
        accountNumber: ba.accountNumber,
        cardIdentifier: ba.cardIdentifier,
        bic: ba.bic,
        bankName: ba.bankName,
        accountHolderName: ba.accountHolderName,
        currencyCode: ba.currencyCode,
        importerKey: ba.importerKey,
        accountId: ba.accountId,
        counterpartyId: ba.counterpartyId,
    };
}

function TextField({
    label,
    value,
    onChange,
    errorName,
    errors,
    autoFocus = false,
}: {
    label: string;
    value: string;
    onChange: (v: string) => void;
    errorName: string;
    errors: Record<string, string[]> | null;
    autoFocus?: boolean;
}) {
    return (
        <label className="flex flex-col gap-1">
            <span className="text-[12px] font-medium text-fg-2">{label}</span>
            <input
                type="text"
                value={value}
                onChange={e => {
                    onChange(e.target.value);
                }}
                autoFocus={autoFocus}
                className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
            />
            <FieldError name={errorName} errors={errors} />
        </label>
    );
}

function RadioOption({
    label,
    checked,
    onChange,
    disabled,
}: {
    label: string;
    checked: boolean;
    onChange: () => void;
    disabled?: boolean;
}) {
    return (
        <label className="inline-flex items-center gap-2 text-[13px] text-fg-1 cursor-pointer">
            <input
                type="radio"
                checked={checked}
                onChange={onChange}
                disabled={disabled}
                className="accent-brand-primary"
            />
            {label}
        </label>
    );
}
