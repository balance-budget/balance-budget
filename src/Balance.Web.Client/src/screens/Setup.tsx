import { useState } from 'react';
import { Form } from 'react-aria-components';
import { useNavigate } from '@tanstack/react-router';
import { useSetup } from '../api/auth';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
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
        <div className="w-full max-w-[480px] bg-bg-1 border border-border-soft rounded-xl shadow-overlay">
            <header className="flex items-center gap-[10px] px-5 pt-5 pb-3">
                <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                <span className="text-lg font-normal tracking-[-0.01em]">
                    Balance<span className="text-brand-primary">.</span>
                </span>
            </header>
            <div className="px-5 pt-1 pb-5">
                <h1 className="text-base font-semibold leading-snug mb-1">First-run setup</h1>
                <p className="text-sm text-fg-3 mb-4">
                    Create the first account. This wizard becomes unavailable once a user exists.
                </p>
                <Form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                >
                    <FormErrorBanner message={errorMessage} />
                    <TextField
                        label="Email"
                        type="email"
                        isRequired
                        autoFocus
                        value={email}
                        onChange={setEmail}
                        className="mb-3"
                    />
                    <TextField
                        label="Display name"
                        isRequired
                        value={displayName}
                        onChange={setDisplayName}
                        className="mb-3"
                    />
                    <TextField
                        label="Password"
                        type="password"
                        isRequired
                        minLength={12}
                        autoComplete="new-password"
                        value={password}
                        onChange={setPassword}
                        description="Minimum 12 characters."
                        className="mb-3"
                    />
                    <div className="flex flex-col gap-1 mb-4">
                        <TextField
                            label="Setup token"
                            value={setupToken}
                            onChange={setSetupToken}
                            inputClassName="font-mono"
                        />
                        <span className="text-xs text-fg-3">
                            Required when configured at deploy time via{' '}
                            <code className="font-mono">Auth:SetupToken</code>.
                        </span>
                    </div>
                    <Button
                        type="submit"
                        variant="primary"
                        isDisabled={setup.isPending}
                        className="w-full"
                    >
                        {setup.isPending ? 'Creating…' : 'Create account'}
                    </Button>
                </Form>
            </div>
        </div>
    );
}
