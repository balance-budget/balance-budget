import { useId, useRef, useState } from 'react';
import { useBankAccounts, useImportStatement, type BankAccount } from '../api/bankAccounts';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { ApiError } from '../lib/http';

function bankAccountLabel(ba: BankAccount): string {
    return ba.bankName ?? ba.iban ?? ba.accountNumber ?? 'Bank account';
}

function bankAccountIdentifier(ba: BankAccount): string | null {
    return ba.iban ?? ba.accountNumber;
}

type ImportFeedback =
    | { kind: 'success'; imported: number; skipped: number }
    | { kind: 'error'; message: string };

function ImportRow({ bankAccount }: { bankAccount: BankAccount }) {
    const inputId = useId();
    const inputRef = useRef<HTMLInputElement>(null);
    const importStatement = useImportStatement();
    const [feedback, setFeedback] = useState<ImportFeedback | null>(null);

    async function onFileChosen(file: File) {
        setFeedback(null);
        try {
            const result = await importStatement.mutateAsync({
                bankAccountId: bankAccount.id,
                file,
            });
            setFeedback({
                kind: 'success',
                imported: result.imported,
                skipped: result.skippedAsDuplicate,
            });
        } catch (err) {
            const message =
                err instanceof ApiError
                    ? err.message
                    : err instanceof Error
                      ? err.message
                      : 'Import failed.';
            setFeedback({ kind: 'error', message });
        } finally {
            // Reset the input so picking the same file again still triggers a change.
            if (inputRef.current) inputRef.current.value = '';
        }
    }

    const identifier = bankAccountIdentifier(bankAccount);
    const isUploading = importStatement.isPending;

    return (
        <div className="py-4 first:pt-0 last:pb-0 flex flex-col gap-2 border-b border-border-soft last:border-b-0">
            <div className="flex items-center gap-3">
                <span className="shrink-0 inline-flex items-center justify-center w-9 h-9 rounded-md bg-brand-primary-soft text-brand-primary">
                    <Icon name="landmark" size={16} strokeWidth={2} />
                </span>
                <div className="flex-1 min-w-0 flex flex-col leading-tight">
                    <span className="text-14 font-medium text-fg-1 truncate">
                        {bankAccountLabel(bankAccount)}
                    </span>
                    <span className="text-[12px] text-fg-3 truncate tabular">
                        {identifier ? `${identifier} · ${bankAccount.currencyCode ?? '—'}` : '—'}
                    </span>
                </div>
                <label
                    htmlFor={inputId}
                    className={
                        'inline-flex items-center gap-2 px-3 py-[7px] rounded-sm select-none ' +
                        'bg-brand-primary text-white text-[13px] font-medium cursor-pointer ' +
                        'hover:bg-brand-primary-dark transition-colors ' +
                        (isUploading ? 'opacity-60 pointer-events-none' : '')
                    }
                >
                    <Icon name="download" size={14} strokeWidth={2} />
                    {isUploading ? 'Importing…' : 'Import statement'}
                </label>
                <input
                    ref={inputRef}
                    id={inputId}
                    type="file"
                    accept=".csv,text/csv,.pdf,application/pdf"
                    className="sr-only"
                    onChange={e => {
                        const file = e.target.files?.[0];
                        if (file) void onFileChosen(file);
                    }}
                    disabled={isUploading}
                />
            </div>
            {feedback?.kind === 'success' && (
                <div className="pl-12 text-[12px] text-success">
                    Imported {feedback.imported}
                    {feedback.skipped > 0
                        ? ` · skipped ${feedback.skipped} duplicate${feedback.skipped === 1 ? '' : 's'}`
                        : ''}
                    .
                </div>
            )}
            {feedback?.kind === 'error' && (
                <div className="pl-12 text-[12px] text-danger">{feedback.message}</div>
            )}
        </div>
    );
}

function ImportsPanel() {
    const bankAccounts = useBankAccounts();

    if (bankAccounts.isPending) {
        return (
            <div className="flex flex-col gap-3">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
            </div>
        );
    }

    if (bankAccounts.isError) {
        return (
            <ErrorState
                message="Couldn't load bank accounts."
                onRetry={() => void bankAccounts.refetch()}
            />
        );
    }

    // Counterparty-owned BankAccounts can't receive imports (accountId is null) —
    // hide them rather than render a disabled row the user can't act on.
    const targets = bankAccounts.data.filter(ba => ba.accountId !== null);

    if (targets.length === 0) {
        return (
            <span className="text-[13px] text-fg-3">
                No bank accounts linked to one of your own accounts yet.
            </span>
        );
    }

    return (
        <div>
            {targets.map(ba => (
                <ImportRow key={ba.id} bankAccount={ba} />
            ))}
        </div>
    );
}

export function BankImports() {
    return (
        <Panel>
            <SectionHead
                title="Import statements"
                subtitle="Upload an ING current-account CSV against the matching bank account. Re-uploads are safe — duplicate rows are skipped."
            />
            <ImportsPanel />
        </Panel>
    );
}
