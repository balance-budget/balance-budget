import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import {
    useCreateToken,
    useRevokeToken,
    useTokens,
    type CreatedToken,
    type Token,
} from '../api/admin';
import { ApiError } from '../lib/http';

export const Route = createFileRoute('/settings/tokens')({
    component: TokensPage,
    staticData: { title: 'API tokens' },
});

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` alongside the component; that's the documented pattern.
function TokensPage() {
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
        <div className="flex flex-col gap-6">
            {justCreated ? (
                <section className="bg-amber-50 border border-amber-300 rounded-2xl p-6">
                    <h2 className="text-base font-semibold mb-2 text-amber-900">
                        Copy your token now
                    </h2>
                    <p className="text-sm text-amber-900 mb-3">
                        This is the only time you will see <em>{justCreated.metadata.name}</em> in
                        full. Store it somewhere safe — you cannot retrieve it again.
                    </p>
                    <code className="block bg-white border border-amber-300 rounded-md px-3 py-2 font-mono text-xs break-all">
                        {justCreated.token}
                    </code>
                    <button
                        type="button"
                        onClick={() => {
                            setJustCreated(null);
                        }}
                        className="mt-3 text-xs underline text-amber-900"
                    >
                        I've copied it, dismiss
                    </button>
                </section>
            ) : null}

            <section className="bg-white rounded-2xl shadow p-6">
                <h2 className="text-base font-semibold mb-3">Tokens</h2>
                {tokensQuery.isLoading ? (
                    <p className="text-sm text-neutral-500">Loading…</p>
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
                    <p className="text-sm text-neutral-500">No tokens yet.</p>
                )}
            </section>

            <section className="bg-white rounded-2xl shadow p-6">
                <h2 className="text-base font-semibold mb-3">Create token</h2>
                <form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                    className="flex flex-col gap-3 max-w-md"
                >
                    <input
                        type="text"
                        required
                        placeholder="name (e.g. ING importer cron)"
                        value={name}
                        onChange={e => {
                            setName(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2 text-sm"
                    />
                    <label className="flex flex-col gap-1 text-xs">
                        Expires (optional)
                        <input
                            type="datetime-local"
                            value={expiresAt}
                            onChange={e => {
                                setExpiresAt(e.target.value);
                            }}
                            className="border border-neutral-300 rounded-md px-3 py-2 text-sm"
                        />
                    </label>
                    {createError ? <p className="text-sm text-rose-600">{createError}</p> : null}
                    <button
                        type="submit"
                        disabled={create.isPending}
                        className="bg-neutral-900 text-white rounded-md py-2 text-sm font-medium disabled:opacity-50"
                    >
                        {create.isPending ? 'Creating…' : 'Create token'}
                    </button>
                </form>
            </section>
        </div>
    );
}

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` alongside the component; that's the documented pattern.
function TokenRow({ token, onRevoke }: { token: Token; onRevoke: () => void }) {
    const isRevoked = !!token.revokedAt;
    const isExpired = token.expiresAt ? new Date(token.expiresAt) <= new Date() : false;
    return (
        <li className="flex items-center justify-between border border-neutral-200 rounded-md px-3 py-2 text-sm">
            <div>
                <div className="font-medium">{token.name}</div>
                <div className="text-xs text-neutral-500 font-mono">
                    {token.prefix}…{token.last4}
                </div>
                <div className="text-xs text-neutral-500">
                    Created {new Date(token.createdAt).toLocaleDateString()}
                    {token.lastUsedAt
                        ? `, last used ${new Date(token.lastUsedAt).toLocaleDateString()}`
                        : ', never used'}
                    {token.expiresAt
                        ? `, expires ${new Date(token.expiresAt).toLocaleDateString()}`
                        : ''}
                </div>
            </div>
            <div className="flex items-center gap-2">
                {isRevoked ? (
                    <span className="text-xs text-rose-600">Revoked</span>
                ) : isExpired ? (
                    <span className="text-xs text-neutral-500">Expired</span>
                ) : (
                    <button
                        type="button"
                        onClick={onRevoke}
                        className="text-xs px-2 py-1 border border-neutral-300 rounded"
                    >
                        Revoke
                    </button>
                )}
            </div>
        </li>
    );
}
