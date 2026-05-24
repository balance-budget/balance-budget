import { useState } from 'react';
import { useCreateAccount, useUpdateAccount, type Account } from '../api/accounts';
import { useCurrencies } from '../api/currencies';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Modal, ModalFooter } from '../components/Modal';
import { useToast } from '../components/Toast';
import type { AccountType } from '../lib/domain';
import { ApiError } from '../lib/http';

const ACCOUNT_TYPES: AccountType[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];

type Props = { onClose: () => void } & ({ mode: 'create' } | { mode: 'edit'; account: Account });

export function AccountFormModal(props: Props) {
    const create = useCreateAccount();
    const update = useUpdateAccount();
    const toast = useToast();
    const currencies = useCurrencies();

    const initial =
        props.mode === 'edit'
            ? {
                  name: props.account.name,
                  accountType: props.account.type,
                  currencyCode: props.account.currencyCode,
              }
            : { name: '', accountType: 'Asset' as AccountType, currencyCode: '' };

    const [name, setName] = useState(initial.name);
    const [accountType, setAccountType] = useState<AccountType>(initial.accountType);
    const [currencyCode, setCurrencyCode] = useState(initial.currencyCode);
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const isPending = create.isPending || update.isPending;
    const currencyList = Array.from(currencies.data?.values() ?? []);

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        try {
            if (props.mode === 'create') {
                await create.mutateAsync({ name, accountType, currencyCode });
            } else {
                await update.mutateAsync({
                    id: props.account.id,
                    original: {
                        name: props.account.name,
                        accountType: props.account.type,
                        currencyCode: props.account.currencyCode,
                    },
                    edited: { name, accountType, currencyCode },
                });
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

    return (
        <Modal
            open
            onClose={props.onClose}
            title={props.mode === 'create' ? 'New account' : 'Edit account'}
            width="sm"
        >
            <form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                noValidate
            >
                <FormErrorBanner message={topError} />

                <label className="flex flex-col gap-1 mb-3">
                    <span className="text-[12px] font-medium text-fg-2">Name</span>
                    <input
                        type="text"
                        value={name}
                        onChange={e => {
                            setName(e.target.value);
                        }}
                        required
                        maxLength={200}
                        autoFocus
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                    />
                    <FieldError name="Name" errors={fieldErrors} />
                </label>

                <label className="flex flex-col gap-1 mb-3">
                    <span className="text-[12px] font-medium text-fg-2">Type</span>
                    <select
                        value={accountType}
                        onChange={e => {
                            setAccountType(e.target.value as AccountType);
                        }}
                        required
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                    >
                        {ACCOUNT_TYPES.map(t => (
                            <option key={t} value={t}>
                                {t}
                            </option>
                        ))}
                    </select>
                    <FieldError name="AccountType" errors={fieldErrors} />
                </label>

                <label className="flex flex-col gap-1">
                    <span className="text-[12px] font-medium text-fg-2">Currency</span>
                    <select
                        value={currencyCode}
                        onChange={e => {
                            setCurrencyCode(e.target.value);
                        }}
                        required
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                    >
                        <option value="">Select…</option>
                        {currencyList.map(c => (
                            <option key={c.code} value={c.code}>
                                {c.code} — {c.name}
                            </option>
                        ))}
                    </select>
                    <FieldError name="CurrencyCode" errors={fieldErrors} />
                </label>

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
