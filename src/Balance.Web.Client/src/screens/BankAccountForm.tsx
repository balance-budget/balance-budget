import { useState } from 'react';
import { useCreateBankAccount, useUpdateBankAccount, type BankAccount } from '../api/bankAccounts';
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
    iban: string;
    accountNumber: string;
    bic: string;
    bankName: string;
    accountHolderName: string;
    currencyCode: string;
    ownerKind: OwnerKind;
    accountId: string;
    counterpartyId: string;
};

function initialState(props: Props): FormState {
    if (props.mode === 'edit') {
        const ba = props.bankAccount;
        const ownerKind: OwnerKind = ba.accountId !== null ? 'account' : 'counterparty';
        return {
            iban: ba.iban ?? '',
            accountNumber: ba.accountNumber ?? '',
            bic: ba.bic ?? '',
            bankName: ba.bankName ?? '',
            accountHolderName: ba.accountHolderName ?? '',
            currencyCode: ba.currencyCode ?? '',
            ownerKind,
            accountId: ba.accountId ?? '',
            counterpartyId: ba.counterpartyId ?? '',
        };
    }
    const pre = props.ownerPrefill;
    const ownerKind: OwnerKind = pre && 'accountId' in pre ? 'account' : 'counterparty';
    return {
        iban: '',
        accountNumber: '',
        bic: '',
        bankName: '',
        accountHolderName: '',
        currencyCode: '',
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

    const [form, setForm] = useState<FormState>(() => initialState(props));
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const ownerLocked = props.mode === 'create' && props.ownerPrefill !== undefined;
    const isPending = create.isPending || update.isPending;

    function update_(patch: Partial<FormState>) {
        setForm(prev => ({ ...prev, ...patch }));
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
                    iban: emptyToNull(form.iban),
                    accountNumber: emptyToNull(form.accountNumber),
                    bic: emptyToNull(form.bic),
                    bankName: emptyToNull(form.bankName),
                    accountHolderName: emptyToNull(form.accountHolderName),
                    currencyCode: payloadCurrency,
                    accountId: accountIdValue,
                    counterpartyId: counterpartyIdValue,
                });
            } else {
                const original = bankAccountToUpdateInput(props.bankAccount);
                const edited = {
                    iban: emptyToNull(form.iban),
                    accountNumber: emptyToNull(form.accountNumber),
                    bic: emptyToNull(form.bic),
                    bankName: emptyToNull(form.bankName),
                    accountHolderName: emptyToNull(form.accountHolderName),
                    currencyCode: payloadCurrency,
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
                    <legend className="text-[12px] font-medium text-fg-2 mb-2">Owner</legend>
                    <div className="flex gap-4 mb-2">
                        <RadioOption
                            label="Account"
                            checked={form.ownerKind === 'account'}
                            disabled={ownerLocked}
                            onChange={() => {
                                update_({ ownerKind: 'account' });
                            }}
                        />
                        <RadioOption
                            label="Counterparty"
                            checked={form.ownerKind === 'counterparty'}
                            disabled={ownerLocked}
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
                    <TextField
                        label="IBAN"
                        value={form.iban}
                        onChange={v => {
                            update_({ iban: v });
                        }}
                        errorName="Iban"
                        errors={fieldErrors}
                        autoFocus
                    />
                    <TextField
                        label="Account number"
                        value={form.accountNumber}
                        onChange={v => {
                            update_({ accountNumber: v });
                        }}
                        errorName="AccountNumber"
                        errors={fieldErrors}
                    />
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
                </div>

                <p className="mt-3 text-[12px] text-fg-3">
                    At least one of IBAN or Account number is required.
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
        iban: ba.iban,
        accountNumber: ba.accountNumber,
        bic: ba.bic,
        bankName: ba.bankName,
        accountHolderName: ba.accountHolderName,
        currencyCode: ba.currencyCode,
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
