import { useLingui } from '@lingui/react/macro';
import { X } from 'lucide-react';
import {
    Button,
    Text,
    UNSTABLE_Toast as AriaToast,
    UNSTABLE_ToastContent as ToastContent,
    UNSTABLE_ToastQueue as ToastQueue,
    UNSTABLE_ToastRegion as ToastRegion,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { Icon, type IconName } from '../Icon';

/*
 * React Aria's Toast is still exported under UNSTABLE_ prefixes (RAC 1.18).
 * The unstable imports are confined to this file (ADR-0024) so stabilization
 * is a one-file rename; the rest of the app talks to the `useToast()` facade.
 */

type ToastVariant = 'success' | 'error' | 'info';

type AppToast = {
    message: string;
    variant: ToastVariant;
};

// React Aria recommends a minimum of 5 seconds for auto-dismissed toasts.
const DISMISS_MS = 5000;

const queue = new ToastQueue<AppToast>();

function push(message: string, variant: ToastVariant = 'info') {
    queue.add({ message, variant }, { timeout: DISMISS_MS });
}

const api = {
    push,
    success: (message: string) => {
        push(message, 'success');
    },
    error: (message: string) => {
        push(message, 'error');
    },
};

export type ToastApi = typeof api;

/** Module-level facade — the queue lives outside React, no provider needed. */
// eslint-disable-next-line react-refresh/only-export-components -- hook lives alongside the region; splitting would just trade a refresh hint for an extra file.
export function useToast(): ToastApi {
    return api;
}

const VARIANT_CLASS: Record<ToastVariant, string> = {
    success: 'bg-success-soft text-success border-success/30',
    error: 'bg-danger-soft text-danger border-danger/30',
    info: 'bg-surface-2 text-fg-1 border-border-soft',
};

const VARIANT_ICON: Record<ToastVariant, IconName> = {
    success: 'check-circle',
    error: 'alert-circle',
    info: 'info',
};

/** Render once at the root of the app. */
export function AppToastRegion() {
    const { t } = useLingui();
    return (
        <ToastRegion
            queue={queue}
            className="fixed top-4 right-4 z-50 flex flex-col-reverse gap-2 outline-none"
        >
            {({ toast }) => (
                <AriaToast
                    toast={toast}
                    className={cx(
                        'flex items-center gap-2 px-3 py-2 rounded-lg border backdrop-blur-md',
                        'text-sm font-medium shadow-overlay min-w-[240px] max-w-[360px] outline-none',
                        VARIANT_CLASS[toast.content.variant],
                    )}
                >
                    <Icon
                        name={VARIANT_ICON[toast.content.variant]}
                        size={16}
                        strokeWidth={2}
                        aria-hidden
                    />
                    <ToastContent className="flex-1 min-w-0">
                        <Text slot="title">{toast.content.message}</Text>
                    </ToastContent>
                    <Button
                        slot="close"
                        aria-label={t`Close`}
                        className="shrink-0 p-[2px] rounded-sm outline-none opacity-70 data-[hovered]:opacity-100 data-[focus-visible]:ring-1 data-[focus-visible]:ring-current"
                    >
                        <X size={13} strokeWidth={2} aria-hidden="true" />
                    </Button>
                </AriaToast>
            )}
        </ToastRegion>
    );
}
