import { ApiError } from './http';

/**
 * The app-wide form error-routing policy, in one place: FluentValidation field
 * errors render inline (`setFieldErrors`), other 4xx surface in a form-level
 * banner (`setTopError`), and 5xx / network / non-ApiError failures go to a
 * toast. Forms call this from their submit `catch`.
 */
export function handleFormError(
    err: unknown,
    handlers: {
        setFieldErrors: (errors: Record<string, string[]>) => void;
        setTopError: (message: string) => void;
        toast: (message: string) => void;
    },
): void {
    if (err instanceof ApiError) {
        if (err.fieldErrors) {
            handlers.setFieldErrors(err.fieldErrors);
        } else if (err.status >= 400 && err.status < 500) {
            handlers.setTopError(err.message);
        } else {
            handlers.toast(err.message);
        }
    } else if (err instanceof Error) {
        handlers.toast(err.message);
    }
}

/**
 * Error routing for destructive/action flows that have no field-level surface:
 * 4xx (e.g. "still referenced" conflicts) render in a local inline message,
 * everything else goes to a toast. Returns nothing; both sinks are callbacks.
 */
export function handleActionError(
    err: unknown,
    handlers: { setError: (message: string) => void; toast: (message: string) => void },
): void {
    if (err instanceof ApiError && err.status >= 400 && err.status < 500) {
        handlers.setError(err.message);
    } else if (err instanceof Error) {
        handlers.toast(err.message);
    }
}
