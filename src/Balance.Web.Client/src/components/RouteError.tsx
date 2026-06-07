import { Button } from 'react-aria-components';
import { useRouter, type ErrorComponentProps } from '@tanstack/react-router';

/**
 * Fallback rendered by TanStack Router when a route's load or render throws.
 * Mirrors ErrorState in style but takes the full viewport so the user knows
 * the page failed rather than a single panel.
 */
export function RouteError({ error, reset }: ErrorComponentProps) {
    const router = useRouter();
    return (
        <div className="flex-1 flex items-center justify-center p-8">
            <div className="max-w-md flex flex-col items-center gap-4 text-center">
                <h2 className="text-22 font-semibold text-fg-1">Something went wrong</h2>
                <p className="text-14 text-fg-3">
                    {error.message || 'An unexpected error occurred while loading this page.'}
                </p>
                <Button
                    onPress={() => {
                        reset();
                        void router.invalidate();
                    }}
                    className="h-9 px-4 inline-flex items-center rounded-sm bg-surface-2 border border-border-soft text-13 font-medium text-fg-1 cursor-pointer outline-none data-[hovered]:bg-surface-3 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary"
                >
                    Try again
                </Button>
            </div>
        </div>
    );
}
