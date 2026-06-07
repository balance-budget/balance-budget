import { X } from 'lucide-react';
import type { ReactNode } from 'react';
import { Dialog, Heading, Modal as AriaModal, ModalOverlay } from 'react-aria-components';
import { cx } from '../../lib/cx';
import { IconButton } from './Button';

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
 * App modal on React Aria's Modal/Dialog. ESC and the close button dismiss;
 * click-outside is intentionally not wired (avoids losing a half-filled form)
 * — which is React Aria's `isDismissable` default.
 */
export function Modal({ open, onClose, title, description, children, width = 'md' }: ModalProps) {
    return (
        <ModalOverlay
            isOpen={open}
            onOpenChange={isOpen => {
                if (!isOpen) onClose();
            }}
            className={
                'fixed inset-0 z-50 flex items-center justify-center p-4 overflow-y-auto ' +
                'bg-surface-overlay backdrop-blur-sm ' +
                'data-[entering]:opacity-0 data-[exiting]:opacity-0 transition-opacity duration-fast'
            }
        >
            <AriaModal className={cx('w-[calc(100vw-32px)]', WIDTH_CLASS[width])}>
                <Dialog
                    className="flex flex-col bg-bg-1 border border-border-soft rounded-md shadow-overlay outline-none text-fg-1"
                    aria-label={title}
                >
                    <header className="flex items-start gap-3 px-5 pt-4 pb-3 border-b border-border-soft">
                        <div className="flex-1 min-w-0 flex flex-col gap-[2px]">
                            <Heading slot="title" className="text-16 font-semibold leading-snug">
                                {title}
                            </Heading>
                            {description !== undefined && (
                                <p className="text-13 text-fg-3">{description}</p>
                            )}
                        </div>
                        <IconButton
                            onPress={onClose}
                            aria-label="Close"
                            className="shrink-0 -mr-1 -mt-1"
                        >
                            <X size={16} strokeWidth={2} aria-hidden="true" />
                        </IconButton>
                    </header>
                    <div className="px-5 py-4">{children}</div>
                </Dialog>
            </AriaModal>
        </ModalOverlay>
    );
}

/** Footer band — keeps actions visually grouped at the bottom of the modal body. */
export function ModalFooter({ children }: { children: ReactNode }) {
    return <div className="flex items-center justify-end gap-2 mt-5">{children}</div>;
}
