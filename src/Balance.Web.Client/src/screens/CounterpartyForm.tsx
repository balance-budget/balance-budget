import { useState } from 'react';
import {
    useCreateCounterparty,
    useUpdateCounterparty,
    type Counterparty,
} from '../api/counterparties';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Modal, ModalFooter } from '../components/Modal';
import { useToast } from '../components/Toast';
import { handleFormError } from '../lib/formErrors';

type Props = { onClose: () => void } & (
    | { mode: 'create' }
    | { mode: 'edit'; counterparty: Counterparty }
);

export function CounterpartyFormModal(props: Props) {
    const create = useCreateCounterparty();
    const update = useUpdateCounterparty();
    const toast = useToast();

    const initialName = props.mode === 'edit' ? props.counterparty.name : '';
    const [name, setName] = useState(initialName);
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const isPending = create.isPending || update.isPending;

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        try {
            if (props.mode === 'create') {
                await create.mutateAsync({ name });
            } else {
                await update.mutateAsync({
                    id: props.counterparty.id,
                    original: { name: props.counterparty.name },
                    edited: { name },
                });
            }
            props.onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal
            open
            onClose={props.onClose}
            title={props.mode === 'create' ? 'New counterparty' : 'Edit counterparty'}
            width="sm"
        >
            <form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                noValidate
            >
                <FormErrorBanner message={topError} />
                <label className="flex flex-col gap-1">
                    <span className="text-12 font-medium text-fg-2">Name</span>
                    <input
                        type="text"
                        value={name}
                        onChange={e => {
                            setName(e.target.value);
                        }}
                        required
                        maxLength={200}
                        autoFocus
                        className="px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 focus:outline-none focus:border-border-strong"
                    />
                    <FieldError name="Name" errors={fieldErrors} />
                </label>
                <ModalFooter>
                    <button
                        type="button"
                        onClick={props.onClose}
                        disabled={isPending}
                        className="px-3 py-[7px] rounded-sm text-13 font-medium text-fg-2 hover:text-fg-1 disabled:opacity-60"
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        disabled={isPending}
                        className="px-3 py-[7px] rounded-sm text-13 font-medium text-white bg-brand-primary hover:bg-brand-primary-dark disabled:opacity-60"
                    >
                        {isPending ? 'Saving…' : props.mode === 'create' ? 'Create' : 'Save'}
                    </button>
                </ModalFooter>
            </form>
        </Modal>
    );
}
