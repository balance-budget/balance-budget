import { useMemo, useState } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import { Form } from 'react-aria-components';
import { useAccounts } from '../api/accounts';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useCreateTemplate,
    useUpdateTemplate,
    type Cadence,
    type JournalEntryTemplate,
    type TemplateCandidate,
    type WireCreateTemplateRequest,
} from '../api/outlook';
import { AccountSelect } from '../components/AccountSelect';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Button } from '../components/ui/Button';
import { Checkbox } from '../components/ui/Checkbox';
import { DatePicker } from '../components/ui/DatePicker';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { NumberField } from '../components/ui/NumberField';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { todayIso } from '../lib/dates';
import { type AccountId, type AccountType, type CounterpartyId } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';

const CADENCES: Cadence[] = ['Once', 'Weekly', 'Monthly', 'Quarterly', 'Yearly'];
const DEFAULT_CADENCE: Cadence = 'Monthly';
type Direction = 'out' | 'in';

const isCreditNormal = (type: AccountType): boolean =>
    type === 'Liability' || type === 'Income' || type === 'Equity';

/** Inverse of the ledger Sign convention: a balance-direction amount → raw JournalLine amount. */
function toRawAmount(direction: Direction, magnitudeMinor: number, type: AccountType): number {
    const balanceDelta = direction === 'in' ? magnitudeMinor : -magnitudeMinor;
    return isCreditNormal(type) ? -balanceDelta : balanceDelta;
}

function fromRawAmount(
    raw: number,
    type: AccountType,
): { direction: Direction; magnitude: number } {
    const balanceDelta = isCreditNormal(type) ? -raw : raw;
    return { direction: balanceDelta >= 0 ? 'in' : 'out', magnitude: Math.abs(balanceDelta) };
}

export function JournalEntryTemplateForm({
    template,
    candidate,
    defaultAccountId,
    onClose,
}: {
    template?: JournalEntryTemplate;
    candidate?: TemplateCandidate;
    /** Pre-selects the pinned account for a fresh "New" (the account being viewed in Outlook). */
    defaultAccountId?: AccountId;
    onClose: () => void;
}) {
    const { t } = useLingui();
    const toast = useToast();
    const catalog = useCurrencyCatalog();
    const accounts = useAccounts();
    const create = useCreateTemplate();
    const update = useUpdateTemplate();
    const isEdit = template !== undefined;

    const seed = template ?? candidate;
    const [name, setName] = useState(template?.name ?? candidate?.suggestedName ?? '');
    const [accountId, setAccountId] = useState<AccountId | null>(
        seed?.accountId ?? defaultAccountId ?? null,
    );
    const [counterAccountId, setCounterAccountId] = useState<AccountId | null>(
        seed?.counterAccountId ?? null,
    );
    const [cadence, setCadence] = useState<Cadence>(seed?.cadence ?? DEFAULT_CADENCE);
    const [anchorDate, setAnchorDate] = useState(seed?.anchorDate ?? todayIso());
    const [hasEnd, setHasEnd] = useState(template?.endDate != null);
    const [endDate, setEndDate] = useState(template?.endDate ?? todayIso());
    const [direction, setDirection] = useState<Direction>('out');
    const [magnitude, setMagnitude] = useState<number>(0);

    // Detection metadata threaded through from a candidate so the matching key survives.
    const counterpartyId: CounterpartyId | null = seed?.counterpartyId ?? null;
    const mandateId = seed?.mandateId ?? null;
    const sepaCreditorId = seed?.sepaCreditorId ?? null;

    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const liquidAccounts = useMemo(
        () =>
            (accounts.data ?? []).filter(
                a => a.isLiquid && (a.type === 'Asset' || a.type === 'Liability'),
            ),
        [accounts.data],
    );
    const selectedAccount = useMemo(
        () => (accounts.data ?? []).find(a => a.id === accountId) ?? null,
        [accounts.data, accountId],
    );
    const scale = selectedAccount
        ? (catalog.get(selectedAccount.currencyCode)?.minorUnitScale ?? 2)
        : 2;

    // One-time seeding of the amount fields once the account (and thus its type) is known.
    const [amountSeeded, setAmountSeeded] = useState(false);
    if (!amountSeeded && selectedAccount && seed) {
        const { direction: d, magnitude: m } = fromRawAmount(
            seed.expectedAmount,
            selectedAccount.type,
        );
        setDirection(d);
        setMagnitude(m / 10 ** (catalog.get(selectedAccount.currencyCode)?.minorUnitScale ?? 2));
        setAmountSeeded(true);
    }

    async function submit() {
        setTopError(null);
        setFieldErrors(null);

        if (accountId === null || selectedAccount === null) {
            setTopError(t`Pick the account this lands on.`);
            return;
        }
        const magnitudeMinor = Math.round(magnitude * 10 ** scale);
        if (magnitudeMinor <= 0) {
            setTopError(t`Enter an expected amount.`);
            return;
        }
        const expectedAmount = toRawAmount(direction, magnitudeMinor, selectedAccount.type);

        try {
            if (isEdit) {
                await update.mutateAsync({
                    id: template.id,
                    request: {
                        name,
                        accountId,
                        counterAccountId,
                        counterpartyId,
                        cadence,
                        anchorDate,
                        endDate: hasEnd ? endDate : null,
                        expectedAmount,
                    },
                });
                toast.success(t`Recurring item updated.`);
            } else {
                const request: WireCreateTemplateRequest = {
                    name,
                    accountId,
                    counterAccountId,
                    counterpartyId,
                    cadence,
                    anchorDate,
                    endDate: hasEnd ? endDate : null,
                    expectedAmount,
                    mandateId,
                    sepaCreditorId,
                };
                await create.mutateAsync(request);
                toast.success(t`Recurring item added.`);
            }
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    const pending = create.isPending || update.isPending;

    return (
        <Modal
            open
            onClose={onClose}
            title={isEdit ? t`Edit recurring item` : t`New recurring item`}
        >
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />

                <div className="flex flex-col gap-4">
                    <TextField
                        label={t`Name`}
                        name="Name"
                        value={name}
                        onChange={setName}
                        isRequired
                        maxLength={128}
                        placeholder={t`e.g. Rent, Salary, Netflix…`}
                    />

                    <div className="flex flex-col gap-1">
                        <label className="text-sm font-medium text-fg-2">
                            <Trans>Account</Trans>
                        </label>
                        <Select
                            aria-label={t`Account`}
                            value={accountId}
                            onChange={key => {
                                setAccountId(key === null ? null : (String(key) as AccountId));
                            }}
                        >
                            {liquidAccounts.map(a => (
                                <SelectItem key={a.id} id={a.id}>
                                    {a.name}
                                </SelectItem>
                            ))}
                        </Select>
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                        <div className="flex flex-col gap-1">
                            <label className="text-sm font-medium text-fg-2">
                                <Trans>Direction</Trans>
                            </label>
                            <Select
                                aria-label={t`Direction`}
                                value={direction}
                                onChange={key => {
                                    setDirection(String(key) as Direction);
                                }}
                            >
                                <SelectItem id="out">{t`Money out`}</SelectItem>
                                <SelectItem id="in">{t`Money in`}</SelectItem>
                            </Select>
                        </div>
                        <NumberField
                            label={t`Expected amount`}
                            value={magnitude}
                            onChange={setMagnitude}
                            minValue={0}
                            formatOptions={{
                                minimumFractionDigits: 0,
                                maximumFractionDigits: scale,
                            }}
                        />
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                        <div className="flex flex-col gap-1">
                            <label className="text-sm font-medium text-fg-2">
                                <Trans>Cadence</Trans>
                            </label>
                            <Select
                                aria-label={t`Cadence`}
                                value={cadence}
                                onChange={key => {
                                    setCadence(String(key) as Cadence);
                                }}
                            >
                                {CADENCES.map(c => (
                                    <SelectItem key={c} id={c}>
                                        {c}
                                    </SelectItem>
                                ))}
                            </Select>
                        </div>
                        <DatePicker
                            label={cadence === 'Once' ? t`Date` : t`First / next date`}
                            value={anchorDate}
                            onChange={setAnchorDate}
                        />
                    </div>

                    <div className="flex flex-col gap-1">
                        <label className="text-sm font-medium text-fg-2">
                            <Trans>Counter account (optional)</Trans>
                        </label>
                        <AccountSelect
                            value={counterAccountId}
                            onChange={setCounterAccountId}
                            onClear={() => {
                                setCounterAccountId(null);
                            }}
                            postableOnly
                            noneLabel={t`None`}
                            ariaLabel={t`Counter account`}
                        />
                    </div>

                    {cadence !== 'Once' && (
                        <div className="flex flex-col gap-2">
                            <Checkbox isSelected={hasEnd} onChange={setHasEnd}>
                                <Trans>Has an end date</Trans>
                            </Checkbox>
                            {hasEnd && (
                                <DatePicker
                                    label={t`End date`}
                                    value={endDate}
                                    onChange={setEndDate}
                                />
                            )}
                        </div>
                    )}
                </div>

                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={pending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={pending}>
                        {isEdit ? <Trans>Save</Trans> : <Trans>Add</Trans>}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}
