import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import { getJson, postJsonNoContent, putJsonNoContent, refreshAntiforgeryToken } from '../lib/http';

type WireCurrentUser = components['schemas']['CurrentUserResponse'];
type WireLoginRequest = components['schemas']['LoginRequest'];
type WireSetupRequest = components['schemas']['SetupRequest'];
type WireUpdatePreferences = components['schemas']['UpdateUserPreferencesRequest'];

export type CurrentUser = {
    id: string;
    email: string;
    displayName: string;
    authScheme: string;
    // Display preferences (ADR-0022); null means "use the default".
    language: string | null;
    dateFormat: string | null;
    numberFormat: string | null;
    theme: string | null;
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
        language: wire.language,
        dateFormat: wire.dateFormat,
        numberFormat: wire.numberFormat,
        theme: wire.theme,
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
            // Identity changed — the antiforgery token cached before login is now invalid.
            await refreshAntiforgeryToken(ctrl.signal);
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

export function useUpdatePreferences() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireUpdatePreferences) => {
            const ctrl = new AbortController();
            await putJsonNoContent(
                '/api/auth/me/preferences',
                request,
                ctrl.signal,
                'save preferences',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: authKeys.me });
        },
    });
}

export function useSetup() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: WireSetupRequest) => {
            const ctrl = new AbortController();
            await postJsonNoContent('/api/auth/setup', request, ctrl.signal, 'set up');
            await refreshAntiforgeryToken(ctrl.signal);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: authKeys.me });
        },
    });
}
