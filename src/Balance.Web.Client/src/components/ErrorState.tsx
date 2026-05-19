type ErrorStateProps = {
    message?: string;
    onRetry?: () => void;
};

export function ErrorState({ message = 'Something went wrong.', onRetry }: ErrorStateProps) {
    return (
        <div className="flex flex-col gap-2 p-3 rounded-sm bg-danger-soft text-danger">
            <span className="text-[13px] font-medium">{message}</span>
            {onRetry && (
                <button
                    type="button"
                    onClick={onRetry}
                    className="self-start text-[12px] font-medium underline hover:no-underline"
                >
                    Retry
                </button>
            )}
        </div>
    );
}
