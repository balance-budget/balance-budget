import { useState } from 'react';
import { Form } from 'react-aria-components';
import { useCreateAccount, useUpdateAccount, type Account } from '../api/accounts';
import { useCurrencies } from '../api/currencies';
import { AccountIconPicker } from '../components/AccountIconPicker';
import { AccountSelect } from '../components/AccountSelect';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { Checkbox } from '../components/ui/Checkbox';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
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
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />

                <div className="flex flex-col gap-1 mb-3">
                    <span className="text-xs font-medium text-fg-2">Icon</span>
                    <AccountIconPicker accountType={accountType} value={icon} onChange={setIcon} />
                    <FieldError name="IconName" errors={fieldErrors} />
                </div>

                <TextField
                    label="Name"
                    name="Name"
                    value={name}
                    onChange={setName}
                    isRequired
                    maxLength={200}
                    autoFocus
                    className="mb-3"
                />

                <TextField
                    label="Code"
                    name="Code"
                    value={code}
                    onChange={setCode}
                    isRequired
                    maxLength={32}
                    placeholder="e.g. 5110"
                    inputClassName="tabular-nums"
                    className="mb-3"
                />

                <Select
                    label="Type"
                    name="AccountType"
                    value={accountType}
                    onChange={key => {
                        setAccountType(key as AccountType);
                        setParentId(null);
                    }}
                    className="mb-3"
                >
                    {ACCOUNT_TYPES.map(t => (
                        <SelectItem key={t} id={t}>
                            {t}
                        </SelectItem>
                    ))}
                </Select>

                <Select
                    label="Currency"
                    name="CurrencyCode"
                    value={currencyCode === '' ? null : currencyCode}
                    onChange={key => {
                        setCurrencyCode(key === null ? '' : String(key));
                        setParentId(null);
                    }}
                    isRequired
                    placeholder="Select…"
                    className="mb-3"
                >
                    {currencyList.map(c => (
                        <SelectItem key={c.code} id={c.code}>
                            {c.code} — {c.name}
                        </SelectItem>
                    ))}
                </Select>

                <div className="flex flex-col gap-1 mb-3">
                    <span className="text-xs font-medium text-fg-2">Parent account</span>
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
                    <span className="text-xs text-fg-3">
                        Only non-postable accounts of the same type and currency can be parents.
                    </span>
                    <FieldError name="ParentAccountId" errors={fieldErrors} />
                </div>

                <Checkbox isSelected={isPostable} onChange={setIsPostable}>
                    <span className="flex flex-col">
                        <span className="text-xs font-medium text-fg-2">
                            Can contain transactions
                        </span>
                        <span className="text-xs text-fg-3">
                            Uncheck to make this a roll-up account that only totals its children.
                        </span>
                    </span>
                </Checkbox>

                <ModalFooter>
                    <Button variant="ghost" onPress={props.onClose} isDisabled={isPending}>
                        Cancel
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={isPending}>
                        {isPending ? 'Saving…' : props.mode === 'create' ? 'Create' : 'Save'}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}
