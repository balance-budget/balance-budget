import { useLingui } from '@lingui/react/macro';
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
    confirmLabel,
    cancelLabel,
    variant = 'primary',
    busy = false,
    error = null,
}: ConfirmDialogProps) {
    const { t } = useLingui();
    return (
        <Modal open={open} onClose={onClose} title={title} width="sm">
            <FormErrorBanner message={error} />
            {message ? <p className="text-sm text-fg-2">{message}</p> : null}
            <ModalFooter>
                <Button variant="ghost" onPress={onClose} isDisabled={busy}>
                    {cancelLabel ?? t`Cancel`}
                </Button>
                <Button
                    variant={variant === 'destructive' ? 'danger' : 'primary'}
                    onPress={onConfirm}
                    isDisabled={busy}
                >
                    {busy ? t`Working…` : (confirmLabel ?? t`Confirm`)}
                </Button>
            </ModalFooter>
        </Modal>
    );
}
