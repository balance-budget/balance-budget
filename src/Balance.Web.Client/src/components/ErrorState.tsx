import { Button } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
type ErrorStateProps = {
    message?: string;
    onRetry?: () => void;
};

export function ErrorState({ message, onRetry }: ErrorStateProps) {
    const { t } = useLingui();
    return (
        <div className="flex flex-col gap-2 p-3 rounded-lg bg-danger-soft text-danger">
            <span className="text-sm font-medium">{message ?? t`Something went wrong.`}</span>
            {onRetry && (
                <Button
                    onPress={onRetry}
                    className="self-start text-xs font-medium underline cursor-pointer outline-none data-[hovered]:no-underline data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary rounded-sm"
                >
                    <Trans>Retry</Trans>
                </Button>
            )}
        </div>
    );
}
