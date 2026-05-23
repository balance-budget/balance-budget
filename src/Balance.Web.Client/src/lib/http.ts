/**
 * Thin fetch helper that surfaces backend RFC 9457 ProblemDetails as a typed
 * Error. The label is used in the error message when the server didn't supply
 * a useful detail/title (e.g. raw 5xx, network failure).
 */
export class ApiError extends Error {
    readonly status: number;
    readonly code: string | null;

    constructor(message: string, status: number, code: string | null) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.code = code;
    }
}

type ProblemDetails = {
    title?: string;
    detail?: string;
    status?: number;
    code?: string;
};

export async function getJson<T>(url: string, signal: AbortSignal, label: string): Promise<T> {
    const response = await fetch(url, { signal });
    if (!response.ok) {
        throw await toApiError(response, label);
    }
    return (await response.json()) as T;
}

async function toApiError(response: Response, label: string): Promise<ApiError> {
    const fallback = `Failed to ${label} (${response.status})`;
    const contentType = response.headers.get('content-type') ?? '';
    if (!contentType.includes('json')) {
        return new ApiError(fallback, response.status, null);
    }
    try {
        const problem = (await response.json()) as ProblemDetails;
        const message = problem.detail ?? problem.title ?? fallback;
        return new ApiError(message, response.status, problem.code ?? null);
    } catch {
        return new ApiError(fallback, response.status, null);
    }
}
