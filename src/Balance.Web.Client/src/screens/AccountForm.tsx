import { useState } from 'react';
import { useCreateAccount, useUpdateAccount, type Account } from '../api/accounts';
import { useCurrencies } from '../api/currencies';
import { AccountIconPicker } from '../components/AccountIconPicker';
import { AccountSelect } from '../components/AccountSelect';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Modal, ModalFooter } from '../components/Modal';
import { useToast } from '../components/Toast';
import type { AccountType } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';

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
                  code: props.account.code,
                  accountType: props.account.type,
                  currencyCode: props.account.currencyCode,
                  isPostable: props.account.isPostable,
                  parentId: props.account.parentId,
                  icon: props.account.icon,
              }
            : {
                  name: '',
                  code: '',
                  accountType: 'Asset' as AccountType,
                  currencyCode: '',
                  isPostable: true,
                  parentId: null as Account['parentId'],
                  icon: null as Account['icon'],
              };

    const [name, setName] = useState(initial.name);
    const [code, setCode] = useState(initial.code);
    const [accountType, setAccountType] = useState<AccountType>(initial.accountType);
    const [currencyCode, setCurrencyCode] = useState(initial.currencyCode);
    const [isPostable, setIsPostable] = useState(initial.isPostable);
    const [parentId, setParentId] = useState<Account['parentId']>(initial.parentId);
    const [icon, setIcon] = useState<Account['icon']>(initial.icon);
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const isPending = create.isPending || update.isPending;
    const currencyList = Array.from(currencies.data?.values() ?? []);

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        try {
            if (props.mode === 'create') {
                await create.mutateAsync({
                    name,
                    code,
                    accountType,
                    currencyCode,
                    isPostable,
                    parentAccountId: parentId,
                    iconName: icon,
                });
            } else {
                await update.mutateAsync({
                    id: props.account.id,
                    original: {
                        name: props.account.name,
                        code: props.account.code,
                        accountType: props.account.type,
                        currencyCode: props.account.currencyCode,
                        isPostable: props.account.isPostable,
                        parentAccountId: props.account.parentId,
                        iconName: props.account.icon,
                    },
                    edited: {
                        name,
                        code,
                        accountType,
                        currencyCode,
                        isPostable,
                        parentAccountId: parentId,
                        iconName: icon,
                    },
                });
            }
            props.onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
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

                <div className="flex flex-col gap-1 mb-3">
                    <span className="text-12 font-medium text-fg-2">Icon</span>
                    <AccountIconPicker accountType={accountType} value={icon} onChange={setIcon} />
                    <FieldError name="IconName" errors={fieldErrors} />
                </div>

                <label className="flex flex-col gap-1 mb-3">
                    <span className="text-12 font-medium text-fg-2">Name</span>
                    <input
                        type="text"
                        value={name}
                        onChange={e => {
                            setName(e.target.value);
                        }}
                        required
                        maxLength={200}
                        autoFocus
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                    />
                    <FieldError name="Name" errors={fieldErrors} />
                </label>

                <label className="flex flex-col gap-1 mb-3">
                    <span className="text-12 font-medium text-fg-2">Code</span>
                    <input
                        type="text"
                        value={code}
                        onChange={e => {
                            setCode(e.target.value);
                        }}
                        required
                        maxLength={32}
                        placeholder="e.g. 5110"
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 tabular focus:outline-none focus:border-border-strong"
                    />
                    <FieldError name="Code" errors={fieldErrors} />
                </label>

                <label className="flex flex-col gap-1 mb-3">
                    <span className="text-12 font-medium text-fg-2">Type</span>
                    <select
                        value={accountType}
                        onChange={e => {
                            setAccountType(e.target.value as AccountType);
                            setParentId(null);
                        }}
                        required
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                    >
                        {ACCOUNT_TYPES.map(t => (
                            <option key={t} value={t}>
                                {t}
                            </option>
                        ))}
                    </select>
                    <FieldError name="AccountType" errors={fieldErrors} />
                </label>

                <label className="flex flex-col gap-1 mb-3">
                    <span className="text-12 font-medium text-fg-2">Currency</span>
                    <select
                        value={currencyCode}
                        onChange={e => {
                            setCurrencyCode(e.target.value);
                            setParentId(null);
                        }}
                        required
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
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

                <div className="flex flex-col gap-1 mb-3">
                    <span className="text-12 font-medium text-fg-2">Parent account</span>
                    {/* Eligible parents: non-postable placeholders sharing the chosen type and
                     *  currency. In edit mode the account's own subtree is excluded so it can't
                     *  become its own ancestor; deeper cycles are rejected server-side too. */}
                    <AccountSelect
                        value={parentId}
                        onChange={id => {
                            setParentId(id);
                        }}
                        onClear={() => {
                            setParentId(null);
                        }}
                        placeholdersOnly
                        type={accountType}
                        currencyCode={currencyCode || undefined}
                        excludeSubtreeOf={props.mode === 'edit' ? props.account.id : undefined}
                        disabled={currencyCode === ''}
                        noneLabel="None — top level"
                        placeholder="None — top level"
                        ariaLabel="Parent account"
                    />
                    <span className="text-11 text-fg-3">
                        Only non-postable accounts of the same type and currency can be parents.
                    </span>
                    <FieldError name="ParentAccountId" errors={fieldErrors} />
                </div>

                <label className="flex items-start gap-2">
                    <input
                        type="checkbox"
                        checked={isPostable}
                        onChange={e => {
                            setIsPostable(e.target.checked);
                        }}
                        className="mt-[3px]"
                    />
                    <span className="flex flex-col">
                        <span className="text-12 font-medium text-fg-2">
                            Can contain transactions
                        </span>
                        <span className="text-11 text-fg-3">
                            Uncheck to make this a roll-up account that only totals its children.
                        </span>
                    </span>
                </label>

                <ModalFooter>
                    <button
                        type="button"
                        onClick={props.onClose}
                        disabled={isPending}
                        className="px-3 py-[7px] rounded-sm text-13 font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={isPending}
                        className="px-3 py-[7px] rounded-sm text-13 font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {isPending ? 'Saving…' : props.mode === 'create' ? 'Create' : 'Save'}
                    </button>
                </ModalFooter>
            </form>
        </Modal>
    );
}
