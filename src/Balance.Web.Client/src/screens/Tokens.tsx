import { useState } from 'react';
import {
    useCreateToken,
    useRevokeToken,
    useTokens,
    type CreatedToken,
    type Token,
} from '../api/admin';
import { Form } from 'react-aria-components';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { formatInstant } from '../i18n/format';
import { ApiError } from '../lib/http';
import { Trans, useLingui } from '@lingui/react/macro';

// Token timestamps are instants — formatted in the browser's local timezone,
// field order following the date preference (ADR-0022).
const TOKEN_DATE: Intl.DateTimeFormatOptions = { year: 'numeric', month: 'short', day: 'numeric' };

export function Tokens() {
    const { t } = useLingui();
    const tokensQuery = useTokens();
    const create = useCreateToken();
    const revoke = useRevokeToken();

    const [name, setName] = useState('');
    const [expiresAt, setExpiresAt] = useState('');
    const [justCreated, setJustCreated] = useState<CreatedToken | null>(null);

    async function submit() {
        try {
            const result = await create.mutateAsync({
                name,
                expiresAt: expiresAt ? new Date(expiresAt).toISOString() : null,
            });
            setJustCreated(result);
            setName('');
            setExpiresAt('');
        } catch {
            /* create.error renders below */
        }
    }

    const createError =
        create.error instanceof ApiError
            ? create.error.message
            : create.error instanceof Error
              ? create.error.message
              : null;

    return (
        <>
            {justCreated ? (
                <Panel className="border-warning/40 bg-warning-soft">
                    <SectionHead
                        title={<Trans>Copy your token now</Trans>}
                        subtitle={
                            <Trans>
                                This is the only time you'll see "{justCreated.metadata.name}" in
                                full. Store it somewhere safe - it can't be retrieved again.
                            </Trans>
                        }
                    />
                    <code className="block px-3 py-2 rounded-lg bg-bg-1 border border-border-soft font-mono text-sm text-fg-1 break-all">
                        {justCreated.token}
                    </code>
                    <div className="mt-3">
                        <Button
                            onPress={() => {
                                setJustCreated(null);
                            }}
                            className="py-[5px] text-xs"
                        >
                            <Trans>I've copied it</Trans>
                        </Button>
                    </div>
                </Panel>
            ) : null}

            <Panel>
                <SectionHead
                    subtitle={
                        <Trans>
                            Personal access tokens for CLI scripts, importers, and third-party
                            callers.
                        </Trans>
                    }
                />
                {tokensQuery.isPending ? (
                    <div className="flex flex-col gap-2">
                        <Skeleton className="h-14" />
                        <Skeleton className="h-14" />
                    </div>
                ) : tokensQuery.data && tokensQuery.data.length > 0 ? (
                    <ul className="flex flex-col gap-2">
                        {tokensQuery.data.map(t => (
                            <TokenRow
                                key={t.id}
                                token={t}
                                onRevoke={() => {
                                    revoke.mutate(t.id);
                                }}
                            />
                        ))}
                    </ul>
                ) : (
                    <p className="text-sm text-fg-3">
                        <Trans>No tokens yet.</Trans>
                    </p>
                )}
            </Panel>

            <Panel>
                <SectionHead title={<Trans>Create token</Trans>} />
                <Form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                    className="flex flex-col max-w-md"
                >
                    <FormErrorBanner message={createError} />
                    <TextField
                        label={t`Name`}
                        isRequired
                        placeholder={t`e.g. ING importer cron`}
                        value={name}
                        onChange={setName}
                        className="mb-3"
                    />
                    <TextField
                        label={t`Expires (optional)`}
                        type="datetime-local"
                        value={expiresAt}
                        onChange={setExpiresAt}
                        className="mb-4"
                    />
                    <div>
                        <Button type="submit" variant="primary" isDisabled={create.isPending}>
                            {create.isPending ? (
                                <Trans>Creating…</Trans>
                            ) : (
                                <Trans>Create token</Trans>
                            )}
                        </Button>
                    </div>
                </Form>
            </Panel>
        </>
    );
}

function TokenRow({ token, onRevoke }: { token: Token; onRevoke: () => void }) {
    const isRevoked = !!token.revokedAt;
    const isExpired = token.expiresAt ? new Date(token.expiresAt) <= new Date() : false;
    return (
        <li className="flex items-center justify-between gap-3 px-3 py-[10px] rounded-lg bg-surface-2 border border-border-soft">
            <div className="min-w-0 flex-1">
                <div className="text-sm font-medium text-fg-1 truncate">{token.name}</div>
                <div className="text-xs text-fg-3 font-mono">
                    {token.prefix}…{token.last4}
                </div>
                <div className="text-xs text-fg-3">
                    <Trans>Created {formatInstant(token.createdAt, TOKEN_DATE)}</Trans>
                    {token.lastUsedAt ? (
                        <Trans> · last used {formatInstant(token.lastUsedAt, TOKEN_DATE)}</Trans>
                    ) : (
                        <Trans> · never used</Trans>
                    )}
                    {token.expiresAt ? (
                        <Trans> · expires {formatInstant(token.expiresAt, TOKEN_DATE)}</Trans>
                    ) : null}
                </div>
            </div>
            <div className="shrink-0">
                {isRevoked ? (
                    <span className="px-2 py-[3px] rounded-sm text-xs font-medium bg-danger-soft text-danger">
                        <Trans>Revoked</Trans>
                    </span>
                ) : isExpired ? (
                    <span className="px-2 py-[3px] rounded-sm text-xs font-medium bg-surface-3 text-fg-3">
                        <Trans>Expired</Trans>
                    </span>
                ) : (
                    <Button onPress={onRevoke} className="py-[5px] text-xs">
                        <Trans>Revoke</Trans>
                    </Button>
                )}
            </div>
        </li>
    );
}
