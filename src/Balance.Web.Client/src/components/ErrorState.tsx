import { Button } from 'react-aria-components';
type ErrorStateProps = {
    message?: string;
    onRetry?: () => void;
};

export function ErrorState({ message = 'Something went wrong.', onRetry }: ErrorStateProps) {
    return (
        <div className="flex flex-col gap-2 p-3 rounded-sm bg-danger-soft text-danger">
            <span className="text-13 font-medium">{message}</span>
            {onRetry && (
                <Button
                    onPress={onRetry}
                    className="self-start text-12 font-medium underline cursor-pointer outline-none data-[hovered]:no-underline data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary rounded-xs"
                >
                    Retry
                </Button>
            )}
        </div>
    );
}
