import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useLogin } from '../api/auth';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { ApiError } from '../lib/http';
import logo from '../assets/logo.svg';

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
            // Falls through to login.error rendering below.
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
        <div className="w-full max-w-[380px] bg-bg-1 border border-border-soft rounded-md shadow-overlay">
            <header className="flex items-center gap-[10px] px-5 pt-5 pb-3">
                <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                <span className="text-[18px] font-normal tracking-[-0.01em]">
                    Balance<span className="text-brand-primary">.</span>
                </span>
            </header>
            <div className="px-5 pt-1 pb-5">
                <h1 className="text-16 font-semibold leading-snug mb-1">Sign in</h1>
                <p className="text-[13px] text-fg-3 mb-4">
                    Enter your email and password to continue.
                </p>
                <form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                    noValidate
                >
                    <FormErrorBanner message={errorMessage} />
                    <label className="flex flex-col gap-1 mb-3">
                        <span className="text-[12px] font-medium text-fg-2">Email</span>
                        <input
                            type="email"
                            required
                            autoComplete="username"
                            autoFocus
                            value={email}
                            onChange={e => {
                                setEmail(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <label className="flex flex-col gap-1 mb-4">
                        <span className="text-[12px] font-medium text-fg-2">Password</span>
                        <input
                            type="password"
                            required
                            autoComplete="current-password"
                            value={password}
                            onChange={e => {
                                setPassword(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <button
                        type="submit"
                        disabled={login.isPending}
                        className="w-full px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {login.isPending ? 'Signing in…' : 'Sign in'}
                    </button>
                </form>
            </div>
        </div>
    );
}
