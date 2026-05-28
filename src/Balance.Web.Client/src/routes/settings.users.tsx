import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { useCurrentUser } from '../api/auth';
import { useCreateUser, useToggleUserActive, useUsers, type User } from '../api/admin';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { ApiError } from '../lib/http';

export const Route = createFileRoute('/settings/users')({
    component: UsersPage,
    staticData: { title: 'Users' },
});

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` alongside the component; that's the documented pattern.
function UsersPage() {
    const usersQuery = useUsers();
    const me = useCurrentUser();
    const createUser = useCreateUser();
    const toggle = useToggleUserActive();

    const [email, setEmail] = useState('');
    const [displayName, setDisplayName] = useState('');
    const [password, setPassword] = useState('');

    async function submit() {
        try {
            await createUser.mutateAsync({ email, displayName, password });
            setEmail('');
            setDisplayName('');
            setPassword('');
        } catch {
            /* createUser.error renders below */
        }
    }

    const createError =
        createUser.error instanceof ApiError
            ? createUser.error.message
            : createUser.error instanceof Error
              ? createUser.error.message
              : null;

    const toggleError =
        toggle.error instanceof ApiError
            ? toggle.error.message
            : toggle.error instanceof Error
              ? toggle.error.message
              : null;

    return (
        <>
            <Panel>
                <SectionHead
                    title="Users"
                    subtitle="Logins that have access to this ledger."
                />
                {usersQuery.isPending ? (
                    <div className="flex flex-col gap-2">
                        <Skeleton className="h-12" />
                        <Skeleton className="h-12" />
                    </div>
                ) : usersQuery.data ? (
                    <ul className="flex flex-col gap-2">
                        {usersQuery.data.map(u => (
                            <UserRow
                                key={u.id}
                                user={u}
                                isSelf={me.data?.id === u.id}
                                onToggle={active => {
                                    toggle.mutate({ id: u.id, active });
                                }}
                            />
                        ))}
                    </ul>
                ) : null}
                {toggleError ? (
                    <div className="mt-3">
                        <FormErrorBanner message={toggleError} />
                    </div>
                ) : null}
            </Panel>

            <Panel>
                <SectionHead title="Add user" />
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
                        <span className="text-[12px] font-medium text-fg-2">Email</span>
                        <input
                            type="email"
                            required
                            value={email}
                            onChange={e => {
                                setEmail(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <label className="flex flex-col gap-1 mb-3">
                        <span className="text-[12px] font-medium text-fg-2">Display name</span>
                        <input
                            type="text"
                            required
                            value={displayName}
                            onChange={e => {
                                setDisplayName(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                    </label>
                    <label className="flex flex-col gap-1 mb-4">
                        <span className="text-[12px] font-medium text-fg-2">Password</span>
                        <input
                            type="password"
                            required
                            minLength={12}
                            value={password}
                            onChange={e => {
                                setPassword(e.target.value);
                            }}
                            className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[14px] focus:outline-none focus:border-border-strong"
                        />
                        <span className="text-[12px] text-fg-3">Minimum 12 characters.</span>
                    </label>
                    <div>
                        <button
                            type="submit"
                            disabled={createUser.isPending}
                            className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                        >
                            {createUser.isPending ? 'Creating…' : 'Create user'}
                        </button>
                    </div>
                </form>
            </Panel>
        </>
    );
}

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` alongside the component; that's the documented pattern.
function UserRow({
    user,
    isSelf,
    onToggle,
}: {
    user: User;
    isSelf: boolean;
    onToggle: (active: boolean) => void;
}) {
    return (
        <li className="flex items-center justify-between gap-3 px-3 py-[10px] rounded-sm bg-surface-2 border border-border-soft">
            <div className="min-w-0">
                <div className="text-[14px] font-medium text-fg-1 truncate">
                    {user.displayName}
                    {isSelf ? (
                        <span className="ml-2 text-[12px] text-fg-3 font-normal">(you)</span>
                    ) : null}
                </div>
                <div className="text-[12px] text-fg-3 truncate">{user.email}</div>
            </div>
            <div className="flex items-center gap-3 shrink-0">
                <span
                    className={
                        user.isActive
                            ? 'px-2 py-[3px] rounded-xs text-[12px] font-medium bg-success-soft text-success'
                            : 'px-2 py-[3px] rounded-xs text-[12px] font-medium bg-danger-soft text-danger'
                    }
                >
                    {user.isActive ? 'Active' : 'Disabled'}
                </span>
                {isSelf ? null : (
                    <button
                        type="button"
                        onClick={() => {
                            onToggle(!user.isActive);
                        }}
                        className="px-3 py-[5px] rounded-sm text-[12px] font-medium text-fg-2 bg-surface-2 border border-border-soft hover:bg-surface-3 hover:text-fg-1"
                    >
                        {user.isActive ? 'Disable' : 'Enable'}
                    </button>
                )}
            </div>
        </li>
    );
}
