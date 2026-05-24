import { Modal, ModalFooter } from './Modal';
import { FormErrorBanner } from './FormErrorBanner';

type ConfirmDialogProps = {
    open: boolean;
    onClose: () => void;
    onConfirm: () => void;
    title: string;
    message?: string;
    confirmLabel?: string;
    cancelLabel?: string;
    variant?: 'destructive' | 'primary';
    busy?: boolean;
    error?: string | null;
};

export function ConfirmDialog({
    open,
    onClose,
    onConfirm,
    title,
    message,
    confirmLabel = 'Confirm',
    cancelLabel = 'Cancel',
    variant = 'primary',
    busy = false,
    error = null,
}: ConfirmDialogProps) {
    const isDestructive = variant === 'destructive';
    return (
        <Modal open={open} onClose={onClose} title={title} width="sm">
            <FormErrorBanner message={error} />
            {message ? <p className="text-[13px] text-fg-2">{message}</p> : null}
            <ModalFooter>
                <button
                    type="button"
                    onClick={onClose}
                    disabled={busy}
                    className="px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                >
                    {cancelLabel}
                </button>
                <button
                    type="button"
                    onClick={onConfirm}
                    disabled={busy}
                    className={
                        'px-3 py-[7px] rounded-sm text-[13px] font-medium text-white disabled:opacity-60 ' +
                        (isDestructive
                            ? 'bg-danger hover:bg-danger-strong'
                            : 'bg-brand-primary hover:bg-brand-primary-dark')
                    }
                >
                    {busy ? 'Working…' : confirmLabel}
                </button>
            </ModalFooter>
        </Modal>
    );
}
