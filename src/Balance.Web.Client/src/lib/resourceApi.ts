import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { compare } from 'fast-json-patch';
import { deleteRequest, getJson, patchJson, postJson } from './http';

/**
 * Builds the standard CRUD hooks for a resource whose endpoints follow the
 * `${basePath}` (collection) / `${basePath}/${id}` (item) convention:
 *
 * - `useDetail(id)` — GET the item and map it through `toView`.
 * - `useCreate()` — POST the create request, map the result, invalidate the resource.
 * - `useUpdate()` — RFC-6902 `compare(original, edited)` PATCH, map, invalidate item + resource.
 * - `useDelete()` — DELETE the item, invalidate the resource.
 *
 * List queries and bespoke endpoints (search/paging, importers, suggestions, …)
 * stay hand-written per resource because their shapes genuinely vary; this only
 * removes the fetch+map+invalidate boilerplate that was identical across
 * resources. Generic over the branded `TId` so query keys and mutate args keep
 * their nominal types (ADR-0004 / ADR-0007).
 */
export function createResourceCrud<
    // TWire is inferred from `toView` and reused to type the getJson/postJson/patchJson fetch
    // generics in the body; it only appears once in the signature but is load-bearing, so the
    // "used once" heuristic doesn't apply here.
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters
    TWire,
    TView,
    TCreate,
    TUpdate extends object,
    TId extends string,
>(config: {
    basePath: string;
    label: string;
    allKey: QueryKey;
    detailKey: (id: TId) => QueryKey;
    toView: (wire: TWire) => TView;
}) {
    const { basePath, label, allKey, detailKey, toView } = config;

    function useDetail(id: TId) {
        return useQuery({
            queryKey: detailKey(id),
            queryFn: async ({ signal }) => {
                const wire = await getJson<TWire>(`${basePath}/${id}`, signal, `load ${label}`);
                return toView(wire);
            },
        });
    }

    function useCreate() {
        const queryClient = useQueryClient();
        return useMutation({
            mutationFn: async (input: TCreate) => {
                const wire = await postJson<TWire>(
                    basePath,
                    input,
                    new AbortController().signal,
                    `create ${label}`,
                );
                return toView(wire);
            },
            onSuccess: async () => {
                await queryClient.invalidateQueries({ queryKey: allKey });
            },
        });
    }

    function useUpdate() {
        const queryClient = useQueryClient();
        return useMutation({
            mutationFn: async (args: { id: TId; original: TUpdate; edited: TUpdate }) => {
                const patch = compare(args.original, args.edited);
                const wire = await patchJson<TWire>(
                    `${basePath}/${args.id}`,
                    patch,
                    new AbortController().signal,
                    `update ${label}`,
                );
                return toView(wire);
            },
            onSuccess: async (_data, vars) => {
                await queryClient.invalidateQueries({ queryKey: allKey });
                await queryClient.invalidateQueries({ queryKey: detailKey(vars.id) });
            },
        });
    }

    function useDelete() {
        const queryClient = useQueryClient();
        return useMutation({
            mutationFn: async (id: TId) => {
                await deleteRequest(
                    `${basePath}/${id}`,
                    new AbortController().signal,
                    `delete ${label}`,
                );
            },
            onSuccess: async () => {
                await queryClient.invalidateQueries({ queryKey: allKey });
            },
        });
    }

    return { useDetail, useCreate, useUpdate, useDelete };
}
