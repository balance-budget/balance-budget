import { useState } from 'react';
import { Form } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
import { useCreateCurrency, useUpdateCurrency, type Currency } from '../api/currencies';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { NumberField } from '../components/ui/NumberField';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { handleFormError } from '../lib/formErrors';

type Props = { onClose: () => void } & ({ mode: 'create' } | { mode: 'edit'; currency: Currency });

const DEFAULT_SCALE = 2;

export function CurrencyFormModal(props: Props) {
    const { t } = useLingui();
    const create = useCreateCurrency();
    const update = useUpdateCurrency();
    const toast = useToast();

    const editing = props.mode === 'edit' ? props.currency : null;
    const [code, setCode] = useState(editing?.code ?? '');
    const [name, setName] = useState(editing?.name ?? '');
    const [minorUnitScale, setMinorUnitScale] = useState(editing?.minorUnitScale ?? DEFAULT_SCALE);
    const [symbol, setSymbol] = useState(editing?.symbol ?? '');
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const isPending = create.isPending || update.isPending;

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        const trimmedSymbol = symbol.trim() === '' ? null : symbol.trim();
        try {
            if (props.mode === 'create') {
                await create.mutateAsync({
                    code,
                    name,
                    minorUnitScale,
                    symbol: trimmedSymbol,
                });
            } else {
                await update.mutateAsync({
                    id: props.currency.code,
                    original: {
                        name: props.currency.name,
                        symbol: props.currency.symbol,
                    },
                    edited: { name, symbol: trimmedSymbol },
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
            title={props.mode === 'create' ? t`New currency` : t`Edit ${code}`}
            width="sm"
        >
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
                className="flex flex-col gap-3"
            >
                <FormErrorBanner message={topError} />
                <TextField
                    label={t`Code`}
                    name="Code"
                    value={code}
                    onChange={value => {
                        setCode(value.toUpperCase());
                    }}
                    isRequired={props.mode === 'create'}
                    isDisabled={props.mode === 'edit'}
                    minLength={2}
                    maxLength={8}
                    pattern="[A-Z][A-Z0-9]*"
                    autoFocus={props.mode === 'create'}
                    inputClassName="tabular-nums uppercase"
                    description={
                        props.mode === 'create'
                            ? t`2–8 uppercase letters or digits, e.g. EUR, USD, BTC.`
                            : t`The code can't be changed after creation.`
                    }
                />
                <TextField
                    label={t`Name`}
                    name="Name"
                    value={name}
                    onChange={setName}
                    isRequired
                    maxLength={64}
                    autoFocus={props.mode === 'edit'}
                />
                <NumberField
                    label={t`Minor-unit scale`}
                    name="MinorUnitScale"
                    value={minorUnitScale}
                    onChange={setMinorUnitScale}
                    isDisabled={props.mode === 'edit'}
                    minValue={0}
                    maxValue={30}
                    description={
                        props.mode === 'create'
                            ? t`Decimal places: 2 for EUR/USD, 0 for JPY, 8 for BTC. Set carefully — to change it later you delete and recreate the currency.`
                            : t`The scale can't be changed after creation.`
                    }
                />
                <TextField
                    label={t`Symbol (optional)`}
                    name="Symbol"
                    value={symbol}
                    onChange={setSymbol}
                    maxLength={8}
                    placeholder={t`e.g. € or $`}
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
