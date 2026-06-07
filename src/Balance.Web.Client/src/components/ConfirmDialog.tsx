import { FormErrorBanner } from './FormErrorBanner';
import { Button } from './ui/Button';
import { Modal, ModalFooter } from './ui/Modal';

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
    return (
        <Modal open={open} onClose={onClose} title={title} width="sm">
            <FormErrorBanner message={error} />
            {message ? <p className="text-13 text-fg-2">{message}</p> : null}
            <ModalFooter>
                <Button variant="ghost" onPress={onClose} isDisabled={busy}>
                    {cancelLabel}
                </Button>
                <Button
                    variant={variant === 'destructive' ? 'danger' : 'primary'}
                    onPress={onConfirm}
                    isDisabled={busy}
                >
                    {busy ? 'Working…' : confirmLabel}
                </Button>
            </ModalFooter>
        </Modal>
    );
}
