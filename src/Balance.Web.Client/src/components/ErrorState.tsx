import { Button } from 'react-aria-components';
type ErrorStateProps = {
    message?: string;
    onRetry?: () => void;
};

export function ErrorState({ message = 'Something went wrong.', onRetry }: ErrorStateProps) {
    return (
        <div className="flex flex-col gap-2 p-3 rounded-lg bg-danger-soft text-danger">
            <span className="text-sm font-medium">{message}</span>
            {onRetry && (
                <Button
                    onPress={onRetry}
                    className="self-start text-xs font-medium underline cursor-pointer outline-none data-[hovered]:no-underline data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary rounded-sm"
                >
                    Retry
                </Button>
            )}
        </div>
    );
}
