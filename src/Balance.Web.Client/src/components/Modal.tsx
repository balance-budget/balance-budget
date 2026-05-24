import { useEffect, useId, useRef, type ReactNode } from 'react';
import { Icon } from './Icon';
import { cx } from '../lib/cx';

type ModalProps = {
    open: boolean;
    onClose: () => void;
    title: string;
    description?: string;
    children: ReactNode;
    width?: 'sm' | 'md' | 'lg';
};

const WIDTH_CLASS: Record<NonNullable<ModalProps['width']>, string> = {
    sm: 'max-w-[380px]',
    md: 'max-w-[480px]',
    lg: 'max-w-[640px]',
};

/**
 * Native <dialog> modal. ESC and the close button dismiss; click-outside is
 * intentionally not wired (avoids losing a half-filled form). Imperatively
 * drives showModal()/close() from the `open` prop so consumers stay declarative.
 */
export function Modal({ open, onClose, title, description, children, width = 'md' }: ModalProps) {
    const dialogRef = useRef<HTMLDialogElement>(null);
    const titleId = useId();
    const descriptionId = useId();

    useEffect(() => {
        const dialog = dialogRef.current;
        if (!dialog) return;
        if (open && !dialog.open) {
            dialog.showModal();
        } else if (!open && dialog.open) {
            dialog.close();
        }
    }, [open]);

    return (
        <dialog
            ref={dialogRef}
            aria-labelledby={titleId}
            aria-describedby={description ? descriptionId : undefined}
            onClose={onClose}
            onCancel={onClose}
            className={cx(
                'p-0 bg-transparent text-fg-1 m-auto',
                'backdrop:bg-surface-overlay backdrop:backdrop-blur-sm',
            )}
        >
            <div
                className={cx(
                    'w-[calc(100vw-32px)] flex flex-col',
                    'bg-bg-1 border border-border-soft rounded-md shadow-overlay',
                    WIDTH_CLASS[width],
                )}
            >
                <header className="flex items-start gap-3 px-5 pt-4 pb-3 border-b border-border-soft">
                    <div className="flex-1 min-w-0 flex flex-col gap-[2px]">
                        <h2 id={titleId} className="text-16 font-semibold leading-snug">
                            {title}
                        </h2>
                        {description ? (
                            <p id={descriptionId} className="text-[13px] text-fg-3">
                                {description}
                            </p>
                        ) : null}
                    </div>
                    <button
                        type="button"
                        onClick={onClose}
                        aria-label="Close"
                        className="shrink-0 -mr-1 -mt-1 p-1 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2"
                    >
                        <Icon name="x" size={16} strokeWidth={2} />
                    </button>
                </header>
                <div className="px-5 py-4">{children}</div>
            </div>
        </dialog>
    );
}

/** Footer band — keeps actions visually grouped at the bottom of the modal body. */
export function ModalFooter({ children }: { children: ReactNode }) {
    return <div className="flex items-center justify-end gap-2 mt-5">{children}</div>;
}
