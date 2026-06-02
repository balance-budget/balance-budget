import { useState } from 'react';
import {
    useCreateToken,
    useRevokeToken,
    useTokens,
    type CreatedToken,
    type Token,
} from '../api/admin';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { ApiError } from '../lib/http';

export function Tokens() {
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
                        title="Copy your token now"
                        subtitle={`This is the only time you'll see "${justCreated.metadata.name}" in full. Store it somewhere safe — it can't be retrieved again.`}
                    />
                    <code className="block px-3 py-2 rounded-sm bg-bg-1 border border-border-soft font-mono text-13 text-fg-1 break-all">
                        {justCreated.token}
                    </code>
                    <div className="mt-3">
                        <button
                            type="button"
                            onClick={() => {
                                setJustCreated(null);
                            }}
                            className="px-3 py-[5px] rounded-sm text-12 font-medium text-fg-2 bg-surface-2 border border-border-soft hover:bg-surface-3 hover:text-fg-1"
                        >
                            I've copied it
                        </button>
                    </div>
                </Panel>
            ) : null}

            <Panel>
                <SectionHead
                    title="API tokens"
                    subtitle="Personal access tokens for CLI scripts, importers, and third-party callers."
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
                    <p className="text-13 text-fg-3">No tokens yet.</p>
                )}
            </Panel>

            <Panel>
                <SectionHead title="Create token" />
                <form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                    noValidate
                    className="flex flex-col max-w-md"
                >
                    <FormErrorBanner message={createError} />
                    <label className="flex flex-col gap-1 mb-3">
                        <span className="text-12 font-medium text-fg-2">Name</span>
                        <input
                            type="text"
                            required
                            placeholder="e.g. ING importer cron"
                            value={name}
                            onChange={e => {
                                setName(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 placeholder:text-fg-4 focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <label className="flex flex-col gap-1 mb-4">
                        <span className="text-12 font-medium text-fg-2">Expires (optional)</span>
                        <input
                            type="datetime-local"
                            value={expiresAt}
                            onChange={e => {
                                setExpiresAt(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <div>
                        <button
                            type="submit"
                            disabled={create.isPending}
                            className="px-3 py-[7px] rounded-sm text-13 font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                        >
                            {create.isPending ? 'Creating…' : 'Create token'}
                        </button>
                    </div>
                </form>
            </Panel>
        </>
    );
}

function TokenRow({ token, onRevoke }: { token: Token; onRevoke: () => void }) {
    const isRevoked = !!token.revokedAt;
    const isExpired = token.expiresAt ? new Date(token.expiresAt) <= new Date() : false;
    return (
        <li className="flex items-center justify-between gap-3 px-3 py-[10px] rounded-sm bg-surface-2 border border-border-soft">
            <div className="min-w-0 flex-1">
                <div className="text-14 font-medium text-fg-1 truncate">{token.name}</div>
                <div className="text-12 text-fg-3 font-mono">
                    {token.prefix}…{token.last4}
                </div>
                <div className="text-12 text-fg-3">
                    Created {new Date(token.createdAt).toLocaleDateString()}
                    {token.lastUsedAt
                        ? ` · last used ${new Date(token.lastUsedAt).toLocaleDateString()}`
                        : ' · never used'}
                    {token.expiresAt
                        ? ` · expires ${new Date(token.expiresAt).toLocaleDateString()}`
                        : ''}
                </div>
            </div>
            <div className="shrink-0">
                {isRevoked ? (
                    <span className="px-2 py-[3px] rounded-xs text-12 font-medium bg-danger-soft text-danger">
                        Revoked
                    </span>
                ) : isExpired ? (
                    <span className="px-2 py-[3px] rounded-xs text-12 font-medium bg-surface-3 text-fg-3">
                        Expired
                    </span>
                ) : (
                    <button
                        type="button"
                        onClick={onRevoke}
                        className="px-3 py-[5px] rounded-sm text-12 font-medium text-fg-2 bg-surface-2 border border-border-soft hover:bg-surface-3 hover:text-fg-1"
                    >
                        Revoke
                    </button>
                )}
            </div>
        </li>
    );
}
