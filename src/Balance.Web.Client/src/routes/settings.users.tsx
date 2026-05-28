import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { useCurrentUser } from '../api/auth';
import { useCreateUser, useToggleUserActive, useUsers, type User } from '../api/admin';
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
        <div className="flex flex-col gap-6">
            <section className="bg-white rounded-2xl shadow p-6">
                <h2 className="text-base font-semibold mb-3">Users</h2>
                {usersQuery.isLoading ? (
                    <p className="text-sm text-neutral-500">Loading…</p>
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
                {toggleError ? <p className="mt-3 text-sm text-rose-600">{toggleError}</p> : null}
            </section>

            <section className="bg-white rounded-2xl shadow p-6">
                <h2 className="text-base font-semibold mb-3">Add user</h2>
                <form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                    className="flex flex-col gap-3 max-w-md"
                >
                    <input
                        type="email"
                        required
                        placeholder="email"
                        value={email}
                        onChange={e => {
                            setEmail(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2 text-sm"
                    />
                    <input
                        type="text"
                        required
                        placeholder="display name"
                        value={displayName}
                        onChange={e => {
                            setDisplayName(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2 text-sm"
                    />
                    <input
                        type="password"
                        required
                        minLength={12}
                        placeholder="password (min 12 chars)"
                        value={password}
                        onChange={e => {
                            setPassword(e.target.value);
                        }}
                        className="border border-neutral-300 rounded-md px-3 py-2 text-sm"
                    />
                    {createError ? <p className="text-sm text-rose-600">{createError}</p> : null}
                    <button
                        type="submit"
                        disabled={createUser.isPending}
                        className="bg-neutral-900 text-white rounded-md py-2 text-sm font-medium disabled:opacity-50"
                    >
                        {createUser.isPending ? 'Creating…' : 'Create user'}
                    </button>
                </form>
            </section>
        </div>
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
        <li className="flex items-center justify-between border border-neutral-200 rounded-md px-3 py-2 text-sm">
            <div>
                <div className="font-medium">
                    {user.displayName}{' '}
                    {isSelf ? <span className="text-xs text-neutral-500">(you)</span> : null}
                </div>
                <div className="text-xs text-neutral-500">{user.email}</div>
            </div>
            <div className="flex items-center gap-2">
                <span
                    className={user.isActive ? 'text-xs text-emerald-600' : 'text-xs text-rose-600'}
                >
                    {user.isActive ? 'Active' : 'Disabled'}
                </span>
                {isSelf ? null : (
                    <button
                        type="button"
                        onClick={() => {
                            onToggle(!user.isActive);
                        }}
                        className="text-xs px-2 py-1 border border-neutral-300 rounded"
                    >
                        {user.isActive ? 'Disable' : 'Enable'}
                    </button>
                )}
            </div>
        </li>
    );
}
