import { useState } from 'react';
import { Form } from 'react-aria-components';
import { useNavigate } from '@tanstack/react-router';
import { useLogin } from '../api/auth';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { ApiError } from '../lib/http';
import logo from '../assets/logo.svg';

export function Login({ returnTo }: { returnTo?: string }) {
    const navigate = useNavigate();
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
        <div className="w-full max-w-[380px] bg-bg-1 border border-border-soft rounded-xl shadow-overlay">
            <header className="flex items-center gap-[10px] px-5 pt-5 pb-3">
                <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                <span className="text-lg font-normal tracking-[-0.01em]">
                    Balance<span className="text-brand-primary">.</span>
                </span>
            </header>
            <div className="px-5 pt-1 pb-5">
                <h1 className="text-base font-semibold leading-snug mb-1">Sign in</h1>
                <p className="text-sm text-fg-3 mb-4">Enter your email and password to continue.</p>
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
                        autoComplete="username"
                        autoFocus
                        value={email}
                        onChange={setEmail}
                        className="mb-3"
                    />
                    <TextField
                        label="Password"
                        type="password"
                        isRequired
                        autoComplete="current-password"
                        value={password}
                        onChange={setPassword}
                        className="mb-4"
                    />
                    <Button
                        type="submit"
                        variant="primary"
                        isDisabled={login.isPending}
                        className="w-full"
                    >
                        {login.isPending ? 'Signing in…' : 'Sign in'}
                    </Button>
                </Form>
            </div>
        </div>
    );
}
