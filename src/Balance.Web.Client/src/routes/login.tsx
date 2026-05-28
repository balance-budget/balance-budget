import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useLogin } from '../api/auth';
import { ApiError } from '../lib/http';

type LoginSearch = { returnTo?: string };

export const Route = createFileRoute('/login')({
    validateSearch: (search: Record<string, unknown>): LoginSearch => ({
        returnTo: typeof search.returnTo === 'string' ? search.returnTo : undefined,
    }),
    component: LoginPage,
    staticData: { title: 'Sign in' },
});

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` alongside the component; that's the documented pattern.
function LoginPage() {
    const navigate = useNavigate();
    const { returnTo } = Route.useSearch();
    const login = useLogin();
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');

    async function submit() {
        try {
            await login.mutateAsync({ email, password });
            await navigate({ to: returnTo ?? '/', replace: true });
        } catch {
            // Error surfaces below via login.error.
        }
    }

    const errorMessage =
        login.error instanceof ApiError
            ? login.error.status === 401
                ? 'Email or password is incorrect.'
                : login.error.message
            : login.error instanceof Error
              ? login.error.message
              : null;

    return (
        <div className="w-full max-w-sm bg-white rounded-2xl shadow p-8">
            <h1 className="text-xl font-semibold mb-2">Sign in to Balance</h1>
            <p className="text-sm text-neutral-500 mb-6">
                Enter your email and password to continue.
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
                        autoComplete="username"
                        value={email}
                        onChange={e => {
                            setEmail(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2"
                    />
                </label>
                <label className="flex flex-col gap-1 text-sm">
                    <span className="font-medium">Password</span>
                    <input
                        type="password"
                        required
                        autoComplete="current-password"
                        value={password}
                        onChange={e => {
                            setPassword(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2"
                    />
                </label>
                {errorMessage ? <div className="text-sm text-rose-600">{errorMessage}</div> : null}
                <button
                    type="submit"
                    disabled={login.isPending}
                    className="bg-neutral-900 text-white rounded-md py-2 font-medium disabled:opacity-50"
                >
                    {login.isPending ? 'Signing in…' : 'Sign in'}
                </button>
            </form>
        </div>
    );
}
