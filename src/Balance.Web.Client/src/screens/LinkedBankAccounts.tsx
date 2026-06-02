import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import {
    bankAccountTypeIcon,
    formatBankAccountLabel,
    formatBankAccountSubline,
    useBankAccounts,
    type BankAccount,
} from '../api/bankAccounts';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Skeleton } from '../components/Skeleton';
import type { AccountId, CounterpartyId } from '../lib/domain';
import { BankAccountFormModal, type BankAccountOwnerPrefill } from './BankAccountForm';

type Owner = { kind: 'account'; id: AccountId } | { kind: 'counterparty'; id: CounterpartyId };

/**
 * Read-only list of BankAccounts owned by a given Account or Counterparty,
 * with a button to create a new one pre-filled with this owner. Reassignment
 * happens via the standalone /settings/bank-accounts CRUD form.
 */
export function LinkedBankAccountsSection({ owner }: { owner: Owner }) {
    const query = useBankAccounts();
    const [creating, setCreating] = useState(false);
    const [reassigning, setReassigning] = useState<BankAccount | null>(null);

    if (query.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-10 w-full" />
            </div>
        );
    }

    if (query.isError) {
        return (
            <ErrorState
                message="Couldn't load bank accounts."
                onRetry={() => void query.refetch()}
            />
        );
    }

    const linked = query.data.filter(ba => belongsTo(ba, owner));
    const prefill: BankAccountOwnerPrefill =
        owner.kind === 'account' ? { accountId: owner.id } : { counterpartyId: owner.id };

    return (
        <>
            <div>
                {linked.length === 0 ? (
                    <div className="py-3 text-13 text-fg-3">No bank accounts linked yet.</div>
                ) : (
                    linked.map(ba => (
                        <LinkedRow
                            key={ba.id}
                            bankAccount={ba}
                            onReassign={() => {
                                setReassigning(ba);
                            }}
                        />
                    ))
                )}
                <div className="pt-3 mt-3 border-t border-border-soft">
                    <button
                        type="button"
                        onClick={() => {
                            setCreating(true);
                        }}
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-13 font-medium text-brand-primary hover:bg-brand-primary-soft"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        Add bank account
                    </button>
                </div>
            </div>
            {creating && (
                <BankAccountFormModal
                    mode="create"
                    ownerPrefill={prefill}
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
            {reassigning && (
                <BankAccountFormModal
                    mode="edit"
                    bankAccount={reassigning}
                    onClose={() => {
                        setReassigning(null);
                    }}
                />
            )}
        </>
    );
}

function belongsTo(ba: BankAccount, owner: Owner): boolean {
    if (owner.kind === 'account') return ba.accountId === owner.id;
    return ba.counterpartyId === owner.id;
}

function LinkedRow({
    bankAccount,
    onReassign,
}: {
    bankAccount: BankAccount;
    onReassign: () => void;
}) {
    return (
        <div className="py-3 first:pt-0 flex items-center gap-3 border-b border-border-soft last:border-b-0">
            <Link
                to="/settings/bank-accounts/$id"
                params={{ id: bankAccount.id }}
                className="flex-1 min-w-0 flex items-center gap-3 hover:text-brand-primary"
            >
                <span className="shrink-0 inline-flex items-center justify-center w-9 h-9 rounded-md bg-brand-primary-soft text-brand-primary">
                    <Icon name={bankAccountTypeIcon(bankAccount.type)} size={16} strokeWidth={2} />
                </span>
                <div className="flex-1 min-w-0 flex flex-col leading-tight">
                    <span className="text-14 font-medium text-fg-1 truncate">
                        {formatBankAccountLabel(bankAccount)}
                    </span>
                    <span className="text-12 text-fg-3 tabular truncate">
                        {formatBankAccountSubline(bankAccount)}
                    </span>
                </div>
            </Link>
            <button
                type="button"
                onClick={onReassign}
                aria-label="Reassign"
                title="Reassign to a different owner"
                className="shrink-0 p-2 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="arrow-left-right" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}
