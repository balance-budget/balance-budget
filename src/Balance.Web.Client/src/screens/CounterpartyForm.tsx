import { useState } from 'react';
import { Form } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
import {
    useCreateCounterparty,
    useUpdateCounterparty,
    type Counterparty,
} from '../api/counterparties';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { handleFormError } from '../lib/formErrors';

type Props = { onClose: () => void } & (
    { mode: 'create' } | { mode: 'edit'; counterparty: Counterparty }
);

export function CounterpartyFormModal(props: Props) {
    const { t } = useLingui();
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
            title={props.mode === 'create' ? t`New counterparty` : t`Edit counterparty`}
            width="sm"
        >
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <TextField
                    label={t`Name`}
                    name="Name"
                    value={name}
                    onChange={setName}
                    isRequired
                    maxLength={200}
                    autoFocus
                />
                <ModalFooter>
                    <Button variant="ghost" onPress={props.onClose} isDisabled={isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={isPending}>
                        {isPending ? (
                            <Trans>Saving…</Trans>
                        ) : props.mode === 'create' ? (
                            <Trans>Create</Trans>
                        ) : (
                            <Trans>Save</Trans>
                        )}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}
