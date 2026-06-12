import { useState } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import { useCurrentUser } from '../api/auth';
import { useCreateUser, useToggleUserActive, useUsers, type User } from '../api/admin';
import { Form } from 'react-aria-components';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { ApiError } from '../lib/http';

export function Users() {
    const { t } = useLingui();
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
                <SectionHead subtitle={<Trans>Logins that have access to this ledger.</Trans>} />
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
                <SectionHead title={<Trans>Add user</Trans>} />
                <Form
                    onSubmit={e => {
                        e.preventDefault();
                        void submit();
                    }}
                    className="flex flex-col max-w-md"
                >
                    <FormErrorBanner message={createError} />
                    <TextField
                        label={t`Email`}
                        type="email"
                        isRequired
                        value={email}
                        onChange={setEmail}
                        className="mb-3"
                    />
                    <TextField
                        label={t`Display name`}
                        isRequired
                        value={displayName}
                        onChange={setDisplayName}
                        className="mb-3"
                    />
                    <TextField
                        label={t`Password`}
                        type="password"
                        isRequired
                        minLength={12}
                        value={password}
                        onChange={setPassword}
                        description={t`Minimum 12 characters.`}
                        className="mb-4"
                    />
                    <div>
                        <Button type="submit" variant="primary" isDisabled={createUser.isPending}>
                            {createUser.isPending ? (
                                <Trans>Creating…</Trans>
                            ) : (
                                <Trans>Create user</Trans>
                            )}
                        </Button>
                    </div>
                </Form>
            </Panel>
        </>
    );
}

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
        <li className="flex items-center justify-between gap-3 px-3 py-[10px] rounded-lg bg-surface-2 border border-border-soft">
            <div className="min-w-0">
                <div className="text-sm font-medium text-fg-1 truncate">
                    {user.displayName}
                    {isSelf ? (
                        <span className="ml-2 text-xs text-fg-3 font-normal">
                            <Trans>(you)</Trans>
                        </span>
                    ) : null}
                </div>
                <div className="text-xs text-fg-3 truncate">{user.email}</div>
            </div>
            <div className="flex items-center gap-3 shrink-0">
                <span
                    className={
                        user.isActive
                            ? 'px-2 py-[3px] rounded-sm text-xs font-medium bg-success-soft text-success'
                            : 'px-2 py-[3px] rounded-sm text-xs font-medium bg-danger-soft text-danger'
                    }
                >
                    {user.isActive ? <Trans>Active</Trans> : <Trans>Disabled</Trans>}
                </span>
                {isSelf ? null : (
                    <Button
                        onPress={() => {
                            onToggle(!user.isActive);
                        }}
                        className="py-[5px] text-xs"
                    >
                        {user.isActive ? <Trans>Disable</Trans> : <Trans>Enable</Trans>}
                    </Button>
                )}
            </div>
        </li>
    );
}
