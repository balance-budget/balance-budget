import { useState } from 'react';
import { FileTrigger } from 'react-aria-components';
import { Link } from '@tanstack/react-router';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import {
    bankAccountTypeIcon,
    formatBankAccountLabel,
    formatBankAccountSubline,
    useBankAccounts,
    useImportStatement,
    type BankAccount,
} from '../api/bankAccounts';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Button } from '../components/ui/Button';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { ApiError } from '../lib/http';

type ImportFeedback =
    | { kind: 'success'; imported: number; skipped: number }
    | { kind: 'error'; message: string };

function ImportRow({ bankAccount }: { bankAccount: BankAccount }) {
    const { t } = useLingui();
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
                      : t`Import failed.`;
            setFeedback({ kind: 'error', message });
        }
    }

    const isUploading = importStatement.isPending;

    return (
        <div className="py-4 first:pt-0 last:pb-0 flex flex-col gap-2 border-b border-border-soft last:border-b-0">
            <div className="flex items-center gap-3">
                <span className="shrink-0 inline-flex items-center justify-center w-9 h-9 rounded-xl bg-brand-primary-soft text-brand-primary">
                    <Icon name={bankAccountTypeIcon(bankAccount.type)} size={16} strokeWidth={2} />
                </span>
                <div className="flex-1 min-w-0 flex flex-col leading-tight">
                    <span className="text-sm font-medium text-fg-1 truncate">
                        {formatBankAccountLabel(bankAccount)}
                    </span>
                    <span className="text-xs text-fg-3 truncate tabular-nums">
                        {formatBankAccountSubline(bankAccount)}
                    </span>
                </div>
                {bankAccount.importerKey === null ? (
                    <Link
                        to="/settings/bank-accounts/$id"
                        params={{ id: bankAccount.id }}
                        className="shrink-0 text-xs text-fg-3 hover:text-fg-1 underline decoration-dotted underline-offset-2"
                        title={t`Set an importer on this bank account to enable statement imports.`}
                    >
                        <Trans>No importer configured</Trans>
                    </Link>
                ) : (
                    <FileTrigger
                        acceptedFileTypes={['.csv', 'text/csv', '.pdf', 'application/pdf']}
                        onSelect={files => {
                            const file = files?.[0];
                            if (file) void onFileChosen(file);
                        }}
                    >
                        <Button variant="primary" isDisabled={isUploading} className="shrink-0">
                            <Icon name="download" size={14} strokeWidth={2} />
                            {isUploading ? t`Importing…` : t`Import statement`}
                        </Button>
                    </FileTrigger>
                )}
            </div>
            {feedback?.kind === 'success' && (
                <div className="pl-12 text-xs text-success">
                    <Trans>Imported {feedback.imported}</Trans>
                    {feedback.skipped > 0 ? (
                        <>
                            {' · '}
                            <Plural
                                value={feedback.skipped}
                                one="skipped # duplicate"
                                other="skipped # duplicates"
                            />
                        </>
                    ) : (
                        ''
                    )}
                    .
                </div>
            )}
            {feedback?.kind === 'error' && (
                <div className="pl-12 text-xs text-danger">{feedback.message}</div>
            )}
        </div>
    );
}

function ImportsPanel() {
    const { t } = useLingui();
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
                message={t`Couldn't load bank accounts.`}
                onRetry={() => void bankAccounts.refetch()}
            />
        );
    }

    // Counterparty-owned BankAccounts can't receive imports (accountId is null) —
    // hide them rather than render a disabled row the user can't act on.
    const targets = bankAccounts.data.filter(ba => ba.accountId !== null);

    if (targets.length === 0) {
        return (
            <span className="text-sm text-fg-3">
                <Trans>No bank accounts linked to one of your own accounts yet.</Trans>
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
    const { t } = useLingui();
    return (
        <Panel>
            <SectionHead
                title={t`Import statements`}
                subtitle={t`Upload an ING current-account CSV against the matching bank account. Re-uploads are safe — duplicate rows are skipped.`}
            />
            <ImportsPanel />
        </Panel>
    );
}
