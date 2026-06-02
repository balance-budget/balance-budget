import { useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useSetup } from '../api/auth';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { ApiError } from '../lib/http';
import logo from '../assets/logo.svg';

export function Setup() {
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
                ? 'Setup is unavailable. Either a user already exists, or the setup token does not match.'
                : setup.error.message
            : setup.error instanceof Error
              ? setup.error.message
              : null;

    return (
        <div className="w-full max-w-[480px] bg-bg-1 border border-border-soft rounded-md shadow-overlay">
            <header className="flex items-center gap-[10px] px-5 pt-5 pb-3">
                <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                <span className="text-18 font-normal tracking-[-0.01em]">
                    Balance<span className="text-brand-primary">.</span>
                </span>
            </header>
            <div className="px-5 pt-1 pb-5">
                <h1 className="text-16 font-semibold leading-snug mb-1">First-run setup</h1>
                <p className="text-13 text-fg-3 mb-4">
                    Create the first account. This wizard becomes unavailable once a user exists.
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
                        <span className="text-12 font-medium text-fg-2">Email</span>
                        <input
                            type="email"
                            required
                            autoFocus
                            value={email}
                            onChange={e => {
                                setEmail(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <label className="flex flex-col gap-1 mb-3">
                        <span className="text-12 font-medium text-fg-2">Display name</span>
                        <input
                            type="text"
                            required
                            value={displayName}
                            onChange={e => {
                                setDisplayName(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <label className="flex flex-col gap-1 mb-3">
                        <span className="text-12 font-medium text-fg-2">Password</span>
                        <input
                            type="password"
                            required
                            minLength={12}
                            autoComplete="new-password"
                            value={password}
                            onChange={e => {
                                setPassword(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                        />
                        <span className="text-12 text-fg-3">Minimum 12 characters.</span>
                    </label>
                    <label className="flex flex-col gap-1 mb-4">
                        <span className="text-12 font-medium text-fg-2">Setup token</span>
                        <input
                            type="text"
                            value={setupToken}
                            onChange={e => {
                                setSetupToken(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 font-mono focus:outline-none focus:border-border-strong"
                        />
                        <span className="text-12 text-fg-3">
                            Required when configured at deploy time via{' '}
                            <code className="font-mono">Auth:SetupToken</code>.
                        </span>
                    </label>
                    <button
                        type="submit"
                        disabled={setup.isPending}
                        className="w-full px-3 py-[7px] rounded-sm text-13 font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {setup.isPending ? 'Creating…' : 'Create account'}
                    </button>
                </form>
            </div>
        </div>
    );
}
