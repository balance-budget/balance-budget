/**
 * Thin fetch helper that surfaces backend RFC 9457 ProblemDetails as a typed
 * Error. The label is used in the error message when the server didn't supply
 * a useful detail/title (e.g. raw 5xx, network failure).
 *
 * `fieldErrors` is populated when the response is a ValidationProblemDetails
 * (FluentValidation 400s) — keys are property paths, values are the messages
 * for that field. Forms render these inline via `<FieldError name="...">`;
 * everything else surfaces in a form-level banner or a toast.
 */
export class ApiError extends Error {
    readonly status: number;
    readonly code: string | null;
    readonly fieldErrors: Record<string, string[]> | null;

    constructor(
        message: string,
        status: number,
        code: string | null,
        fieldErrors: Record<string, string[]> | null,
    ) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.code = code;
        this.fieldErrors = fieldErrors;
    }
}

type ProblemDetails = {
    title?: string;
    detail?: string;
    status?: number;
    code?: string;
    errors?: Record<string, string[]>;
};

/**
 * ASP.NET Core antiforgery uses a paired token: a HttpOnly cookie token plus a *request
 * token* that the client must send as <c>X-XSRF-TOKEN</c> on writes. The request token
 * is delivered as the JSON body of <c>/api/antiforgery/token</c>; we cache it in
 * module memory (not localStorage — XSS-exfiltratable) and refresh whenever the server
 * rejects a write as 400 Bad Request (typically after an identity change).
 */
let cachedRequestToken: string | null = null;
let inflightTokenFetch: Promise<string> | null = null;

async function fetchRequestToken(signal?: AbortSignal): Promise<string> {
    if (inflightTokenFetch) return inflightTokenFetch;
    inflightTokenFetch = (async () => {
        try {
            const response = await fetch('/api/antiforgery/token', {
                ...baseRequestInit,
                signal,
            });
            if (!response.ok) {
                throw new ApiError(
                    `Failed to fetch antiforgery token (${response.status})`,
                    response.status,
                    null,
                    null,
                );
            }
            const body = (await response.json()) as { token: string };
            cachedRequestToken = body.token;
            return body.token;
        } finally {
            inflightTokenFetch = null;
        }
    })();
    return inflightTokenFetch;
}

async function ensureRequestToken(signal?: AbortSignal): Promise<string> {
    if (cachedRequestToken) return cachedRequestToken;
    return fetchRequestToken(signal);
}

function clearRequestTokenCache(): void {
    cachedRequestToken = null;
}

async function withXsrfHeader(
    headers: Record<string, string>,
    signal?: AbortSignal,
): Promise<Record<string, string>> {
    const token = await ensureRequestToken(signal);
    return { ...headers, 'X-XSRF-TOKEN': token };
}

const baseRequestInit: RequestInit = { credentials: 'same-origin' };

export async function getJson<T>(url: string, signal: AbortSignal, label: string): Promise<T> {
    const response = await fetch(url, { ...baseRequestInit, signal });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return (await response.json()) as T;
}

/**
 * Sends a mutating request with the X-XSRF-TOKEN header attached. If the server
 * rejects with 400 (typical when the antiforgery token is identity-bound and the
 * caller's identity has changed since the cached token was issued), the helper
 * refetches the token once and retries before surfacing the failure.
 */
async function sendMutation(
    url: string,
    method: string,
    body: BodyInit | null,
    headers: Record<string, string>,
    label: string,
    signal?: AbortSignal,
): Promise<Response> {
    async function attempt(): Promise<Response> {
        const finalHeaders = await withXsrfHeader(headers, signal);
        return fetch(url, { ...baseRequestInit, method, headers: finalHeaders, body, signal });
    }

    let response = await attempt();
    if (response.status === 400 && cachedRequestToken) {
        // Drain the failing body so the underlying connection is released, then refresh
        // the token and retry exactly once.
        await response.text().catch(() => undefined);
        clearRequestTokenCache();
        response = await attempt();
    }
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return response;
}

export async function postJson<T>(
    url: string,
    body: unknown,
    label: string,
    signal?: AbortSignal,
): Promise<T> {
    const response = await sendMutation(
        url,
        'POST',
        JSON.stringify(body),
        { 'Content-Type': 'application/json' },
        label,
        signal,
    );
    return (await response.json()) as T;
}

export async function postJsonNoContent(
    url: string,
    body: unknown,
    label: string,
    signal?: AbortSignal,
): Promise<void> {
    await sendMutation(
        url,
        'POST',
        JSON.stringify(body),
        { 'Content-Type': 'application/json' },
        label,
        signal,
    );
}

export async function postFormData<T>(
    url: string,
    formData: FormData,
    label: string,
    signal?: AbortSignal,
): Promise<T> {
    const response = await sendMutation(url, 'POST', formData, {}, label, signal);
    return (await response.json()) as T;
}

export async function putJson<T>(
    url: string,
    body: unknown,
    label: string,
    signal?: AbortSignal,
): Promise<T> {
    const response = await sendMutation(
        url,
        'PUT',
        JSON.stringify(body),
        { 'Content-Type': 'application/json' },
        label,
        signal,
    );
    return (await response.json()) as T;
}

export async function putJsonNoContent(
    url: string,
    body: unknown,
    label: string,
    signal?: AbortSignal,
): Promise<void> {
    await sendMutation(
        url,
        'PUT',
        JSON.stringify(body),
        { 'Content-Type': 'application/json' },
        label,
        signal,
    );
}

/**
 * RFC 6902 JSON Patch. `patch` is the array of operations produced by
 * fast-json-patch's `compare()`. Server returns the updated resource.
 */
export async function patchJson<T>(
    url: string,
    patch: unknown,
    label: string,
    signal?: AbortSignal,
): Promise<T> {
    const response = await sendMutation(
        url,
        'PATCH',
        JSON.stringify(patch),
        { 'Content-Type': 'application/json-patch+json' },
        label,
        signal,
    );
    return (await response.json()) as T;
}

/**
 * PATCH with a plain JSON body (not RFC 6902) — for endpoints that take a full
 * replacement payload rather than a patch document. Server returns the updated
 * resource.
 */
export async function patchJsonBody<T>(
    url: string,
    body: unknown,
    label: string,
    signal?: AbortSignal,
): Promise<T> {
    const response = await sendMutation(
        url,
        'PATCH',
        JSON.stringify(body),
        { 'Content-Type': 'application/json' },
        label,
        signal,
    );
    return (await response.json()) as T;
}

export async function deleteRequest(
    url: string,
    label: string,
    signal?: AbortSignal,
): Promise<void> {
    await sendMutation(url, 'DELETE', null, {}, label, signal);
}

/** DELETE that returns the updated parent resource (sub-resource deletes). */
export async function deleteJson<T>(url: string, label: string, signal?: AbortSignal): Promise<T> {
    const response = await sendMutation(url, 'DELETE', null, {}, label, signal);
    return (await response.json()) as T;
}

/**
 * Refreshes the cached antiforgery request token. Call after any operation that changes
 * the caller's identity (login, logout, setup) — the server's tokens are bound to the
 * current user and the previously-cached token will fail validation after a transition.
 */
export async function refreshAntiforgeryToken(signal?: AbortSignal): Promise<void> {
    clearRequestTokenCache();
    await fetchRequestToken(signal);
}

async function toApiError(response: Response, label: string): Promise<ApiError> {
    const fallback = `Failed to ${label} (${response.status})`;
    const contentType = response.headers.get('content-type') ?? '';
    if (!contentType.includes('json')) {
        return new ApiError(fallback, response.status, null, null);
    }
    try {
        const problem = (await response.json()) as ProblemDetails;
        const message = problem.detail ?? problem.title ?? fallback;
        const fieldErrors = problem.errors ?? null;
        return new ApiError(message, response.status, problem.code ?? null, fieldErrors);
    } catch {
        return new ApiError(fallback, response.status, null, null);
    }
}
