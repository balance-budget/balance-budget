import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { getJson, postJson, postJsonNoContent } from '../lib/http';

type WireUser = components['schemas']['UserResponse'];
type WireCreateUser = components['schemas']['CreateUserRequest'];
type WireToken = components['schemas']['TokenResponse'];
type WireCreateToken = components['schemas']['CreateTokenRequest'];
type WireCreatedToken = components['schemas']['CreatedTokenResponse'];

export type User = WireUser;
export type Token = WireToken;
export type CreatedToken = WireCreatedToken;

export const adminKeys = {
    users: ['admin', 'users'] as const,
    tokens: ['admin', 'tokens'] as const,
};

export function useUsers() {
    return useQuery({
        queryKey: adminKeys.users,
        queryFn: ({ signal }) => getJson<WireUser[]>('/api/admin/users', signal, 'list users'),
    });
}

export function useCreateUser() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireCreateUser) => {
            const ctrl = new AbortController();
            return postJson<WireUser>('/api/admin/users', request, ctrl.signal, 'create user');
        },
        onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.users }),
    });
}

export function useToggleUserActive() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ id, active }: { id: string; active: boolean }) => {
            const ctrl = new AbortController();
            const action = active ? 'enable' : 'disable';
            await postJsonNoContent(
                `/api/admin/users/${id}/${action}`,
                {},
                ctrl.signal,
                `${action} user`,
            );
        },
        onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.users }),
    });
}

export function useTokens() {
    return useQuery({
        queryKey: adminKeys.tokens,
        queryFn: ({ signal }) => getJson<WireToken[]>('/api/admin/tokens', signal, 'list tokens'),
    });
}

export function useCreateToken() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireCreateToken) => {
            const ctrl = new AbortController();
            return postJson<WireCreatedToken>(
                '/api/admin/tokens',
                request,
                ctrl.signal,
                'create token',
            );
        },
        onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.tokens }),
    });
}

export function useRevokeToken() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: string) => {
            const ctrl = new AbortController();
            await postJsonNoContent(
                `/api/admin/tokens/${id}/revoke`,
                {},
                ctrl.signal,
                'revoke token',
            );
        },
        onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.tokens }),
    });
}
