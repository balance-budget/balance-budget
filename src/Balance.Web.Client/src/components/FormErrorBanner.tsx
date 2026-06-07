type FormErrorBannerProps = {
    message: string | null;
};

/** Top-of-modal banner for non-field server errors (Invariant, Conflict, NotFound). */
export function FormErrorBanner({ message }: FormErrorBannerProps) {
    if (!message) return null;
    return (
        <div className="mb-3 px-3 py-2 rounded-lg bg-danger-soft text-danger text-sm">
            {message}
        </div>
    );
}
