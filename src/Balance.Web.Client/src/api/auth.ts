import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { getJson, postJsonNoContent, primeAntiforgeryCookie } from '../lib/http';

type WireCurrentUser = components['schemas']['CurrentUserResponse'];
type WireLoginRequest = components['schemas']['LoginRequest'];
type WireSetupRequest = components['schemas']['SetupRequest'];

export type CurrentUser = {
    id: string;
    email: string;
    displayName: string;
    authScheme: string;
};

export const authKeys = {
    me: ['auth', 'me'] as const,
};

function toCurrentUser(wire: WireCurrentUser): CurrentUser {
    return {
        id: wire.id,
        email: wire.email,
        displayName: wire.displayName,
        authScheme: wire.authScheme,
    };
}

export function useCurrentUser() {
    return useQuery({
        queryKey: authKeys.me,
        queryFn: async ({ signal }) => {
            try {
                const wire = await getJson<WireCurrentUser>(
                    '/api/auth/me',
                    signal,
                    'fetch current user',
                );
                return toCurrentUser(wire);
            } catch (err) {
                if (
                    err instanceof Error &&
                    'status' in err &&
                    (err as { status: number }).status === 401
                ) {
                    return null;
                }
                throw err;
            }
        },
        staleTime: 60_000,
        retry: false,
    });
}

export function useLogin() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireLoginRequest) => {
            const ctrl = new AbortController();
            await postJsonNoContent('/api/auth/login', request, ctrl.signal, 'log in');
            // Prime the antiforgery cookie so subsequent writes succeed on the first try.
            await primeAntiforgeryCookie(ctrl.signal);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: authKeys.me });
        },
    });
}

export function useLogout() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async () => {
            const ctrl = new AbortController();
            await postJsonNoContent('/api/auth/logout', {}, ctrl.signal, 'log out');
        },
        onSuccess: () => {
            queryClient.setQueryData(authKeys.me, null);
            queryClient.clear();
        },
    });
}

export function useSetup() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireSetupRequest) => {
            const ctrl = new AbortController();
            await postJsonNoContent('/api/auth/setup', request, ctrl.signal, 'set up');
            await primeAntiforgeryCookie(ctrl.signal);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: authKeys.me });
        },
    });
}
