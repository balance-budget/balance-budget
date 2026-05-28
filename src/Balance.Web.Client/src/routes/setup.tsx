import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useSetup } from '../api/auth';
import { ApiError } from '../lib/http';

export const Route = createFileRoute('/setup')({
    component: SetupPage,
    staticData: { title: 'First-run setup' },
});

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` alongside the component; that's the documented pattern.
function SetupPage() {
    const navigate = useNavigate();
    const setup = useSetup();
    const [email, setEmail] = useState('');
    const [displayName, setDisplayName] = useState('');
    const [password, setPassword] = useState('');
    const [setupToken, setSetupToken] = useState('');

    async function submit() {
        try {
            await setup.mutateAsync({
                email,
                password,
                displayName,
                setupToken: setupToken || null,
            });
            await navigate({ to: '/', replace: true });
        } catch {
            // Falls through to setup.error rendering below.
        }
    }

    const errorMessage =
        setup.error instanceof ApiError
            ? setup.error.status === 404
                ? 'Setup is unavailable. The wizard refused this request — either a user already exists, or the setup token does not match.'
                : setup.error.message
            : setup.error instanceof Error
              ? setup.error.message
              : null;

    return (
        <div className="w-full max-w-md bg-white rounded-2xl shadow p-8">
            <h1 className="text-xl font-semibold mb-2">Set up Balance</h1>
            <p className="text-sm text-neutral-500 mb-6">
                Create the first account. This wizard becomes unavailable as soon as one user
                exists.
            </p>
            <form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                className="flex flex-col gap-4"
            >
                <label className="flex flex-col gap-1 text-sm">
                    <span className="font-medium">Email</span>
                    <input
                        type="email"
                        required
                        value={email}
                        onChange={e => {
                            setEmail(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2"
                    />
                </label>
                <label className="flex flex-col gap-1 text-sm">
                    <span className="font-medium">Display name</span>
                    <input
                        type="text"
                        required
                        value={displayName}
                        onChange={e => {
                            setDisplayName(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2"
                    />
                </label>
                <label className="flex flex-col gap-1 text-sm">
                    <span className="font-medium">Password</span>
                    <input
                        type="password"
                        required
                        minLength={12}
                        autoComplete="new-password"
                        value={password}
                        onChange={e => {
                            setPassword(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2"
                    />
                    <span className="text-xs text-neutral-500">Minimum 12 characters.</span>
                </label>
                <label className="flex flex-col gap-1 text-sm">
                    <span className="font-medium">Setup token</span>
                    <input
                        type="text"
                        value={setupToken}
                        onChange={e => {
                            setSetupToken(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2 font-mono text-xs"
                    />
                    <span className="text-xs text-neutral-500">
                        Required when configured at deploy time via <code>Auth:SetupToken</code>.
                    </span>
                </label>
                {errorMessage ? <div className="text-sm text-rose-600">{errorMessage}</div> : null}
                <button
                    type="submit"
                    disabled={setup.isPending}
                    className="bg-neutral-900 text-white rounded-md py-2 font-medium disabled:opacity-50"
                >
                    {setup.isPending ? 'Creating…' : 'Create account'}
                </button>
            </form>
        </div>
    );
}
