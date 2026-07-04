import { useState } from 'react';
import { DropZone, FileTrigger, Text, type FileDropItem } from 'react-aria-components';
import { Link } from '@tanstack/react-router';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import {
    bankAccountTypeIcon,
    formatBankAccountLabel,
    formatBankAccountSubline,
    useBankAccounts,
    useDetectAndImportStatements,
    useImportStatement,
    type BankAccount,
    type DetectedImportOutcome,
} from '../api/bankAccounts';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Button } from '../components/ui/Button';
import { GridList, GridListItem } from '../components/ui/GridList';
import { Select, SelectItem } from '../components/ui/Select';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { ApiError } from '../lib/http';

type ImportFeedback =
    { kind: 'success'; imported: number; skipped: number } | { kind: 'error'; message: string };

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
        <GridListItem
            id={bankAccount.id}
            textValue={formatBankAccountLabel(bankAccount)}
            className="flex flex-col gap-2 px-3 py-4"
        >
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
        </GridListItem>
    );
}

function isResolvable(status: DetectedImportOutcome['status']): boolean {
    return (
        status === 'NoMatchingAccount' || status === 'AmbiguousMatch' || status === 'NotImportable'
    );
}

// A single dropped file the detector couldn't auto-place: let the user pick an importable
// account and submit it through the per-account route (which re-validates the content).
function UnresolvedFileRow({ file, accounts }: { file: File; accounts: BankAccount[] }) {
    const { t } = useLingui();
    const importStatement = useImportStatement();
    const [bankAccountId, setBankAccountId] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<ImportFeedback | null>(null);

    async function onImport() {
        const target = accounts.find(a => a.id === bankAccountId);
        if (!target) return;
        setFeedback(null);
        try {
            const result = await importStatement.mutateAsync({ bankAccountId: target.id, file });
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

    if (feedback?.kind === 'success') {
        return (
            <div className="pl-9 text-xs text-success">
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
        );
    }

    return (
        <div className="pl-9 flex flex-col gap-2">
            <div className="flex items-end gap-2">
                <Select
                    aria-label={t`Choose a bank account`}
                    className="flex-1 min-w-0"
                    value={bankAccountId}
                    onChange={key => {
                        setBankAccountId(key === null ? null : String(key));
                    }}
                    placeholder={t`Choose a bank account…`}
                >
                    {accounts.map(a => (
                        <SelectItem key={a.id} id={a.id} textValue={formatBankAccountLabel(a)}>
                            {formatBankAccountLabel(a)}
                        </SelectItem>
                    ))}
                </Select>
                <Button
                    variant="secondary"
                    isDisabled={bankAccountId === null || importStatement.isPending}
                    onPress={() => void onImport()}
                >
                    {importStatement.isPending ? t`Importing…` : t`Import`}
                </Button>
            </div>
            {feedback?.kind === 'error' && (
                <div className="text-xs text-danger">{feedback.message}</div>
            )}
        </div>
    );
}

function OutcomeRow({
    outcome,
    file,
    importableAccounts,
}: {
    outcome: DetectedImportOutcome;
    file: File | undefined;
    importableAccounts: BankAccount[];
}) {
    const { t } = useLingui();

    const statusLine = (() => {
        switch (outcome.status) {
            case 'Imported':
                return (
                    <span className="text-success">
                        <Trans>Imported {outcome.imported}</Trans>
                        {outcome.skippedAsDuplicate > 0 ? (
                            <>
                                {' · '}
                                <Plural
                                    value={outcome.skippedAsDuplicate}
                                    one="skipped # duplicate"
                                    other="skipped # duplicates"
                                />
                            </>
                        ) : (
                            ''
                        )}
                    </span>
                );
            case 'NoMatchingAccount':
                return <span className="text-fg-3">{t`No bank account matched this file.`}</span>;
            case 'AmbiguousMatch':
                return (
                    <span className="text-fg-3">{t`This file matched more than one bank account.`}</span>
                );
            case 'NotImportable':
                return (
                    <span className="text-fg-3">{t`The matched bank account has no importer configured.`}</span>
                );
            case 'Unrecognized':
                return (
                    <span className="text-fg-3">{t`This file isn't a recognized statement.`}</span>
                );
            case 'Failed':
                return <span className="text-danger">{t`Import failed.`}</span>;
        }
    })();

    return (
        <div className="py-3 first:pt-0 last:pb-0 flex flex-col gap-2 border-b border-border-soft last:border-b-0">
            <div className="flex items-center gap-3">
                <span className="shrink-0 inline-flex items-center justify-center w-6 h-6 text-fg-3">
                    <Icon
                        name={outcome.status === 'Imported' ? 'check' : 'file'}
                        size={14}
                        strokeWidth={2}
                    />
                </span>
                <span className="flex-1 min-w-0 text-sm text-fg-1 truncate">
                    {outcome.fileName}
                </span>
                <span className="shrink-0 text-xs tabular-nums">{statusLine}</span>
            </div>
            {isResolvable(outcome.status) && file && importableAccounts.length > 0 && (
                <UnresolvedFileRow file={file} accounts={importableAccounts} />
            )}
        </div>
    );
}

function StatementDropZone({ importableAccounts }: { importableAccounts: BankAccount[] }) {
    const { t } = useLingui();
    const detect = useDetectAndImportStatements();
    const [outcomes, setOutcomes] = useState<DetectedImportOutcome[] | null>(null);
    const [files, setFiles] = useState<File[]>([]);
    const [error, setError] = useState<string | null>(null);

    async function runDetect(dropped: File[]) {
        if (dropped.length === 0) return;
        setError(null);
        setFiles(dropped);
        try {
            const result = await detect.mutateAsync(dropped);
            setOutcomes(result);
        } catch (err) {
            setOutcomes(null);
            setError(
                err instanceof ApiError
                    ? err.message
                    : err instanceof Error
                      ? err.message
                      : t`Import failed.`,
            );
        }
    }

    const fileByName = new Map(files.map(f => [f.name, f] as const));

    return (
        <div className="flex flex-col gap-3">
            <DropZone
                getDropOperation={() => 'copy'}
                onDrop={e => {
                    void (async () => {
                        const dropped = await Promise.all(
                            e.items
                                .filter((item): item is FileDropItem => item.kind === 'file')
                                .map(item => item.getFile()),
                        );
                        await runDetect(dropped);
                    })();
                }}
                className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-border-soft bg-surface-2 px-6 py-8 text-center data-[drop-target]:border-brand-primary data-[drop-target]:bg-brand-primary-soft"
            >
                <Text slot="label" className="text-sm text-fg-2">
                    {detect.isPending ? (
                        <Trans>Detecting…</Trans>
                    ) : (
                        <Trans>
                            Drop your statement files here. We'll match each to its account.
                        </Trans>
                    )}
                </Text>
                <FileTrigger
                    allowsMultiple
                    acceptedFileTypes={['.csv', 'text/csv', '.pdf', 'application/pdf']}
                    onSelect={fileList => {
                        if (!fileList) return;
                        void runDetect(Array.from(fileList));
                    }}
                >
                    <Button variant="secondary" isDisabled={detect.isPending}>
                        <Trans>Browse files</Trans>
                    </Button>
                </FileTrigger>
            </DropZone>

            {error && <div className="text-xs text-danger">{error}</div>}

            {outcomes && outcomes.length > 0 && (
                <div className="rounded-xl border border-border-soft px-4 py-2">
                    {outcomes.map((outcome, i) => (
                        <OutcomeRow
                            key={`${outcome.fileName}-${i}`}
                            outcome={outcome}
                            file={fileByName.get(outcome.fileName)}
                            importableAccounts={importableAccounts}
                        />
                    ))}
                </div>
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
        <GridList aria-label={t`Bank accounts`} items={targets}>
            {ba => <ImportRow bankAccount={ba} />}
        </GridList>
    );
}

export function BankImports() {
    const { t } = useLingui();
    const bankAccounts = useBankAccounts();
    const importableAccounts =
        bankAccounts.data?.filter(ba => ba.accountId !== null && ba.importerKey !== null) ?? [];

    return (
        <Panel>
            <SectionHead
                title={t`Import statements`}
                subtitle={t`Drop your statement files and we'll match each to its bank account. Re-uploads are safe. Duplicate rows are skipped.`}
            />
            <div className="flex flex-col gap-6">
                <StatementDropZone importableAccounts={importableAccounts} />
                <div className="flex flex-col gap-2">
                    <h3 className="text-xs font-medium uppercase tracking-wide text-fg-3">
                        <Trans>Or import to a specific account</Trans>
                    </h3>
                    <ImportsPanel />
                </div>
            </div>
        </Panel>
    );
}
