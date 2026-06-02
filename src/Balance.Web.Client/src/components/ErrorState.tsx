type ErrorStateProps = {
    message?: string;
    onRetry?: () => void;
};

export function ErrorState({ message = 'Something went wrong.', onRetry }: ErrorStateProps) {
    return (
        <div className="flex flex-col gap-2 p-3 rounded-sm bg-danger-soft text-danger">
            <span className="text-13 font-medium">{message}</span>
            {onRetry && (
                <button
                    type="button"
                    onClick={onRetry}
                    className="self-start text-12 font-medium underline hover:no-underline"
                >
                    Retry
                </button>
            )}
        </div>
    );
}
