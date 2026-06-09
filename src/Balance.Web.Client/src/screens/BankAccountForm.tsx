import { useState } from 'react';
import { Form } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
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
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { Radio, RadioGroup } from '../components/ui/RadioGroup';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { asAccountId, asCounterpartyId, isLedgerAccount } from '../lib/domain';
import type { AccountId, CounterpartyId } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';

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
    const { t } = useLingui();
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
        // Card BankAccounts are owned-only (ADR 0009). Snap ownerKind back to 'account'
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

        const accountIdValue = form.ownerKind === 'account' ? asAccountId(form.accountId) : null;
        const counterpartyIdValue =
            form.ownerKind === 'counterparty' ? asCounterpartyId(form.counterpartyId) : null;

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
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
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
            title={props.mode === 'create' ? t`New bank account` : t`Edit bank account`}
            width="md"
        >
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />

                <RadioGroup
                    label={t`Type`}
                    name="Type"
                    value={form.type}
                    onChange={value => {
                        changeType(value as BankAccountType);
                    }}
                    className="mb-4"
                >
                    <Radio value="Current">
                        <Trans>Current</Trans>
                    </Radio>
                    <Radio value="Savings">
                        <Trans>Savings</Trans>
                    </Radio>
                    <Radio value="Card">
                        <Trans>Card</Trans>
                    </Radio>
                </RadioGroup>

                <RadioGroup
                    label={t`Owner`}
                    name="OwnerKind"
                    value={form.ownerKind}
                    onChange={value => {
                        update_({ ownerKind: value as OwnerKind });
                    }}
                    isDisabled={ownerKindLocked}
                    className="mb-2"
                >
                    <Radio value="account">
                        <Trans>Account</Trans>
                    </Radio>
                    <Radio value="counterparty">
                        <Trans>Counterparty</Trans>
                    </Radio>
                </RadioGroup>

                <div className="mb-4">
                    {form.ownerKind === 'account' ? (
                        <Select
                            aria-label={t`Owner account`}
                            name="AccountId"
                            value={form.accountId === '' ? null : form.accountId}
                            onChange={key => {
                                update_({ accountId: key === null ? '' : String(key) });
                            }}
                            isDisabled={ownerLocked}
                            isRequired
                            placeholder={t`Select an account…`}
                        >
                            {ledgerAccounts.map(a => (
                                <SelectItem key={a.id} id={a.id}>
                                    {a.name} ({a.type})
                                </SelectItem>
                            ))}
                        </Select>
                    ) : (
                        <Select
                            aria-label={t`Owner counterparty`}
                            name="CounterpartyId"
                            value={form.counterpartyId === '' ? null : form.counterpartyId}
                            onChange={key => {
                                update_({ counterpartyId: key === null ? '' : String(key) });
                            }}
                            isDisabled={ownerLocked}
                            isRequired
                            placeholder={t`Select a counterparty…`}
                        >
                            {counterpartyList.map(c => (
                                <SelectItem key={c.id} id={c.id}>
                                    {c.name}
                                </SelectItem>
                            ))}
                        </Select>
                    )}
                </div>

                <div className="grid grid-cols-2 gap-3">
                    {form.type !== 'Card' ? (
                        <TextField
                            label={`${t`IBAN`}${form.type === 'Current' ? ' *' : ''}`}
                            name="Iban"
                            value={form.iban}
                            onChange={v => {
                                update_({ iban: v });
                            }}
                            autoFocus
                        />
                    ) : null}
                    {form.type === 'Savings' ? (
                        <TextField
                            label={t`Account number`}
                            name="AccountNumber"
                            value={form.accountNumber}
                            onChange={v => {
                                update_({ accountNumber: v });
                            }}
                        />
                    ) : null}
                    {form.type === 'Card' ? (
                        <TextField
                            label={`${t`Card identifier`} *`}
                            name="CardIdentifier"
                            value={form.cardIdentifier}
                            onChange={v => {
                                update_({ cardIdentifier: v });
                            }}
                            autoFocus
                        />
                    ) : null}
                    <TextField
                        label={t`BIC`}
                        name="Bic"
                        value={form.bic}
                        onChange={v => {
                            update_({ bic: v });
                        }}
                    />
                    <TextField
                        label={t`Bank name`}
                        name="BankName"
                        value={form.bankName}
                        onChange={v => {
                            update_({ bankName: v });
                        }}
                    />
                    <TextField
                        label={t`Account holder name`}
                        name="AccountHolderName"
                        value={form.accountHolderName}
                        onChange={v => {
                            update_({ accountHolderName: v });
                        }}
                    />
                    <Select
                        label={form.ownerKind === 'account' ? `${t`Currency`} *` : t`Currency`}
                        name="CurrencyCode"
                        value={form.currencyCode === '' ? null : form.currencyCode}
                        onChange={key => {
                            update_({ currencyCode: key === null ? '' : String(key) });
                        }}
                        isRequired={form.ownerKind === 'account'}
                        placeholder={form.ownerKind === 'account' ? t`Select…` : t`(none)`}
                    >
                        {currencyList.map(c => (
                            <SelectItem key={c.code} id={c.code}>
                                {c.code} — {c.name}
                            </SelectItem>
                        ))}
                    </Select>
                    {form.ownerKind === 'account' ? (
                        <Select
                            label={t`Statement importer`}
                            name="ImporterKey"
                            value={form.importerKey === '' ? null : form.importerKey}
                            onChange={key => {
                                update_({ importerKey: key === null ? '' : String(key) });
                            }}
                            placeholder={t`(none)`}
                        >
                            {importerOptions.map(i => (
                                <SelectItem key={i.key} id={i.key}>
                                    {i.key}
                                </SelectItem>
                            ))}
                        </Select>
                    ) : null}
                </div>

                <p className="mt-3 text-xs text-fg-3">
                    {form.type === 'Current'
                        ? t`IBAN is required.`
                        : form.type === 'Savings'
                          ? t`IBAN or Account number is required.`
                          : t`Card identifier is required. Card accounts are owned-only.`}
                </p>

                <ModalFooter>
                    <Button variant="ghost" onPress={props.onClose} isDisabled={isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={isPending}>
                        {isPending ? t`Saving…` : props.mode === 'create' ? t`Create` : t`Save`}
                    </Button>
                </ModalFooter>
            </Form>
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
