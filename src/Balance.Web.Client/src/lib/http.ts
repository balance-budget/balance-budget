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

export async function getJson<T>(url: string, signal: AbortSignal, label: string): Promise<T> {
    const response = await fetch(url, { signal });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return (await response.json()) as T;
}

export async function postJson<T>(
    url: string,
    body: unknown,
    signal: AbortSignal,
    label: string,
): Promise<T> {
    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        signal,
    });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return (await response.json()) as T;
}

export async function postFormData<T>(
    url: string,
    formData: FormData,
    signal: AbortSignal,
    label: string,
): Promise<T> {
    const response = await fetch(url, { method: 'POST', body: formData, signal });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return (await response.json()) as T;
}

/**
 * RFC 6902 JSON Patch. `patch` is the array of operations produced by
 * fast-json-patch's `compare()`. Server returns the updated resource.
 */
export async function patchJson<T>(
    url: string,
    patch: unknown,
    signal: AbortSignal,
    label: string,
): Promise<T> {
    const response = await fetch(url, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json-patch+json' },
        body: JSON.stringify(patch),
        signal,
    });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return (await response.json()) as T;
}

export async function deleteRequest(
    url: string,
    signal: AbortSignal,
    label: string,
): Promise<void> {
    const response = await fetch(url, { method: 'DELETE', signal });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
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
