import { useMemo, useState } from 'react';
import { Form } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
import { useCounterparties } from '../api/counterparties';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useAddLoanPart,
    useAddRatePeriod,
    useCreateLoan,
    useUpdateLoan,
    useUpdateLoanPart,
    useUpdateRatePeriod,
    type LoanDetail,
    type LoanPart,
    type LoanRatePeriod,
    type RepaymentType,
    type UpdateLoanInput,
    type WireCreateLoanPartRequest,
} from '../api/loans';
import { AccountSelect } from '../components/AccountSelect';
import { FieldError } from '../components/FieldError';
import { FormErrorBanner } from '../components/FormErrorBanner';
import { Icon } from '../components/Icon';
import { Button } from '../components/ui/Button';
import { ComboBox } from '../components/ui/ComboBox';
import { DatePicker } from '../components/ui/DatePicker';
import { Modal, ModalFooter } from '../components/ui/Modal';
import { Radio, RadioGroup } from '../components/ui/RadioGroup';
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { todayIso } from '../lib/dates';
import type { AccountId, CounterpartyId, LoanId, LoanPartId } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';
import {
    buildDeposit,
    buildPartRequest,
    emptyDeposit,
    emptyPart,
    REPAYMENT_TYPES,
    type DepositDraft,
    type PartDraft,
} from './loanForm.state';

/** Optional Construction deposit (ADR-0026): a plain Asset account, an Income account, a rate. */
function ConstructionDepositFields({
    value,
    currencyCode,
    onChange,
}: {
    value: DepositDraft;
    currencyCode: string;
    onChange: (next: DepositDraft) => void;
}) {
    const { t } = useLingui();
    return (
        <div className="rounded-lg border border-border-soft p-3 flex flex-col gap-3 mt-3">
            <div className="flex flex-col gap-0.5">
                <span className="text-sm font-medium text-fg-1">
                    <Trans>Construction deposit (optional)</Trans>
                </span>
                <span className="text-xs text-fg-3">
                    <Trans>
                        Undisbursed mortgage money earmarked for building. Its interest offsets the
                        loan payment during construction.
                    </Trans>
                </span>
            </div>
            <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-fg-2">
                        <Trans>Deposit account</Trans>
                    </span>
                    <AccountSelect
                        value={value.accountId}
                        onChange={accountId => {
                            onChange({ ...value, accountId });
                        }}
                        postableOnly
                        type="Asset"
                        currencyCode={currencyCode}
                        placeholder={t`Asset account holding the deposit…`}
                        ariaLabel={t`Construction deposit account`}
                    />
                </div>
                <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-fg-2">
                        <Trans>Interest income account</Trans>
                    </span>
                    <AccountSelect
                        value={value.incomeAccountId}
                        onChange={incomeAccountId => {
                            onChange({ ...value, incomeAccountId });
                        }}
                        postableOnly
                        type="Income"
                        placeholder={t`Income account for deposit interest…`}
                        ariaLabel={t`Deposit interest income account`}
                    />
                </div>
            </div>
            <TextField
                label={t`Deposit annual rate (%)`}
                value={value.ratePercent}
                onChange={ratePercent => {
                    onChange({ ...value, ratePercent });
                }}
                placeholder={t`e.g. 3.6`}
                inputClassName="tabular-nums"
            />
        </div>
    );
}

export function LoanFormModal({ onClose }: { onClose: () => void }) {
    const { t } = useLingui();
    const create = useCreateLoan();
    const toast = useToast();
    const counterparties = useCounterparties();
    const catalog = useCurrencyCatalog();

    const [name, setName] = useState('');
    const [lenderId, setLenderId] = useState<CounterpartyId | null>(null);
    const [interestAccountId, setInterestAccountId] = useState<AccountId | null>(null);
    const [currencyCode, setCurrencyCode] = useState('EUR');
    const [parentName, setParentName] = useState('');
    const [parentCode, setParentCode] = useState('');
    const [parts, setParts] = useState<PartDraft[]>([emptyPart(0)]);
    const [deposit, setDeposit] = useState<DepositDraft>(emptyDeposit);
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const lenderItems = useMemo(
        () =>
            [...(counterparties.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name))
                .map(c => ({ key: c.id, label: c.name, value: c.id })),
        [counterparties.data],
    );

    const scale = catalog.get(currencyCode)?.minorUnitScale ?? 2;

    function updatePart(id: string, patch: Partial<PartDraft>) {
        setParts(prev => prev.map(p => (p.id === id ? { ...p, ...patch } : p)));
    }

    async function submit() {
        setTopError(null);
        setFieldErrors(null);

        if (lenderId === null) {
            setTopError(t`Pick the lender.`);
            return;
        }
        if (interestAccountId === null) {
            setTopError(t`Pick the interest expense account.`);
            return;
        }

        const partRequests: WireCreateLoanPartRequest[] = [];
        for (const part of parts) {
            const built = buildPartRequest(part, scale);
            if (!built.ok) {
                setTopError(built.error);
                return;
            }
            partRequests.push(built.request);
        }

        const depositResult = buildDeposit(deposit);
        if (!depositResult.ok) {
            setTopError(depositResult.error);
            return;
        }

        try {
            await create.mutateAsync({
                name,
                lenderCounterpartyId: lenderId,
                interestExpenseAccountId: interestAccountId,
                currencyCode,
                parentAccountName: parentName.trim() === '' ? name.trim() : parentName.trim(),
                parentAccountCode: parentCode.trim(),
                parts: partRequests,
                ...depositResult.value,
            });
            toast.success(t`Loan created.`);
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal open onClose={onClose} title={t`New loan`} width="lg">
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />

                <div className="grid grid-cols-2 gap-3 mb-3">
                    <TextField
                        label={t`Name`}
                        name="Name"
                        value={name}
                        onChange={setName}
                        isRequired
                        maxLength={128}
                        autoFocus
                        placeholder={t`e.g. Mortgage Hoofdstraat 1`}
                    />
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            <Trans>Lender</Trans>
                        </span>
                        <ComboBox
                            items={lenderItems}
                            value={lenderId}
                            onChange={setLenderId}
                            placeholder={t`Pick the lender…`}
                            ariaLabel={t`Lender`}
                        />
                        <span className="text-xs text-fg-3">
                            <Trans>Drives the Inbox&apos;s loan-payment hint.</Trans>
                        </span>
                    </div>
                </div>

                <div className="grid grid-cols-2 gap-3 mb-3">
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            <Trans>Interest expense account</Trans>
                        </span>
                        <AccountSelect
                            value={interestAccountId}
                            onChange={setInterestAccountId}
                            postableOnly
                            type="Expense"
                            placeholder={t`e.g. Mortgage interest`}
                            ariaLabel={t`Interest expense account`}
                        />
                        <FieldError name="InterestExpenseAccountId" errors={fieldErrors} />
                    </div>
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            <Trans>Currency</Trans>
                        </span>
                        <Select
                            aria-label={t`Currency`}
                            value={currencyCode}
                            onChange={key => {
                                if (key !== null) setCurrencyCode(String(key));
                            }}
                        >
                            {[...catalog.keys()].map(code => (
                                <SelectItem key={code} id={code}>
                                    {code}
                                </SelectItem>
                            ))}
                        </Select>
                    </div>
                </div>

                <div className="grid grid-cols-2 gap-3 mb-4">
                    <TextField
                        label={t`Parent account name`}
                        name="ParentAccountName"
                        value={parentName}
                        onChange={setParentName}
                        maxLength={128}
                        placeholder={t`Defaults to the loan name`}
                    />
                    <TextField
                        label={t`Parent account code`}
                        name="ParentAccountCode"
                        value={parentCode}
                        onChange={setParentCode}
                        isRequired
                        maxLength={32}
                        placeholder={t`e.g. 2200`}
                        inputClassName="tabular-nums"
                    />
                </div>

                <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-medium text-fg-1">
                        <Trans>Loan parts</Trans>
                    </span>
                    <Button
                        variant="ghost"
                        onPress={() => {
                            setParts(prev => [...prev, emptyPart(prev.length)]);
                        }}
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        <Trans>Add part</Trans>
                    </Button>
                </div>
                <div className="flex flex-col gap-3 mb-2">
                    {parts.map(part => (
                        <PartFields
                            key={part.id}
                            part={part}
                            currencyCode={currencyCode}
                            onChange={patch => {
                                updatePart(part.id, patch);
                            }}
                            onRemove={
                                parts.length > 1
                                    ? () => {
                                          setParts(prev => prev.filter(p => p.id !== part.id));
                                      }
                                    : undefined
                            }
                        />
                    ))}
                </div>

                <ConstructionDepositFields
                    value={deposit}
                    currencyCode={currencyCode}
                    onChange={setDeposit}
                />

                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={create.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={create.isPending}>
                        {create.isPending ? <Trans>Creating…</Trans> : <Trans>Create loan</Trans>}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}

/** One Loan Part's definition: terms, the account (adopt-or-create), and its initial rate. */
function PartFields({
    part,
    currencyCode,
    onChange,
    onRemove,
}: {
    part: PartDraft;
    currencyCode: string;
    onChange: (patch: Partial<PartDraft>) => void;
    onRemove?: () => void;
}) {
    const { t, i18n } = useLingui();
    return (
        <div className="rounded-lg border border-border-soft p-3 flex flex-col gap-3">
            <div className="grid grid-cols-[1fr_1fr_auto] gap-3 items-end">
                <TextField
                    label={t`Label`}
                    value={part.label}
                    onChange={label => {
                        onChange({ label });
                    }}
                    isRequired
                    maxLength={64}
                    placeholder={t`As on the lender's statement`}
                />
                <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-fg-2">
                        <Trans>Repayment type</Trans>
                    </span>
                    <Select
                        aria-label={t`Repayment type`}
                        value={part.repaymentType}
                        onChange={key => {
                            if (key !== null) onChange({ repaymentType: key as RepaymentType });
                        }}
                    >
                        {REPAYMENT_TYPES.map(rt => (
                            <SelectItem key={rt.id} id={rt.id} textValue={i18n._(rt.label)}>
                                {i18n._(rt.label)}
                            </SelectItem>
                        ))}
                    </Select>
                </div>
                {onRemove && (
                    <Button variant="ghost" onPress={onRemove} aria-label={t`Remove part`}>
                        <Icon name="trash" size={14} strokeWidth={2} />
                    </Button>
                )}
            </div>

            <div className="grid grid-cols-2 gap-3">
                <DatePicker
                    label={t`Start date`}
                    value={part.startDate}
                    onChange={startDate => {
                        onChange({ startDate });
                    }}
                />
                <DatePicker
                    label={t`End date`}
                    value={part.endDate}
                    onChange={endDate => {
                        onChange({ endDate });
                    }}
                />
            </div>

            <div className="grid grid-cols-2 gap-3">
                <TextField
                    label={t`Annual rate (%)`}
                    value={part.ratePercent}
                    onChange={ratePercent => {
                        onChange({ ratePercent });
                    }}
                    isRequired
                    placeholder={t`e.g. 3.8`}
                    inputClassName="tabular-nums"
                />
                <DatePicker
                    label={t`Rate fixed until (optional)`}
                    value={part.fixedUntil}
                    onChange={fixedUntil => {
                        onChange({ fixedUntil });
                    }}
                />
            </div>

            <RadioGroup
                aria-label={t`Account source`}
                orientation="horizontal"
                value={part.mode}
                onChange={value => {
                    onChange({ mode: value as PartDraft['mode'] });
                }}
            >
                <Radio value="adopt">
                    <Trans>Adopt an existing account</Trans>
                </Radio>
                <Radio value="new">
                    <Trans>Create a fresh account</Trans>
                </Radio>
            </RadioGroup>

            {part.mode === 'adopt' ? (
                <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-fg-2">
                        <Trans>Liability account</Trans>
                    </span>
                    <AccountSelect
                        value={part.adoptAccountId}
                        onChange={adoptAccountId => {
                            onChange({ adoptAccountId });
                        }}
                        postableOnly
                        type="Liability"
                        currencyCode={currencyCode}
                        placeholder={t`Existing mortgage / loan account…`}
                        ariaLabel={t`Account to adopt`}
                    />
                    <span className="text-xs text-fg-3">
                        <Trans>Re-parented under the loan with its posted history intact.</Trans>
                    </span>
                </div>
            ) : (
                <div className="grid grid-cols-3 gap-3">
                    <TextField
                        label={t`Account name`}
                        value={part.newName}
                        onChange={newName => {
                            onChange({ newName });
                        }}
                        maxLength={128}
                    />
                    <TextField
                        label={t`Account code`}
                        value={part.newCode}
                        onChange={newCode => {
                            onChange({ newCode });
                        }}
                        maxLength={32}
                        inputClassName="tabular-nums"
                    />
                    <TextField
                        label={t`Opening balance (${currencyCode})`}
                        value={part.openingBalance}
                        onChange={openingBalance => {
                            onChange({ openingBalance });
                        }}
                        placeholder={t`Outstanding principal`}
                        inputClassName="tabular-nums"
                    />
                </div>
            )}
        </div>
    );
}

export function AddLoanPartModal({ loan, onClose }: { loan: LoanDetail; onClose: () => void }) {
    const { t } = useLingui();
    const addPart = useAddLoanPart();
    const toast = useToast();
    const catalog = useCurrencyCatalog();
    const [part, setPart] = useState<PartDraft>(() => emptyPart(loan.parts.length));
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const scale = catalog.get(loan.currencyCode)?.minorUnitScale ?? 2;

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        const built = buildPartRequest(part, scale);
        if (!built.ok) {
            setTopError(built.error);
            return;
        }
        try {
            await addPart.mutateAsync({ id: loan.id, request: built.request });
            toast.success(t`Part added.`);
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal open onClose={onClose} title={t`Add loan part`} width="lg">
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <PartFields
                    part={part}
                    currencyCode={loan.currencyCode}
                    onChange={patch => {
                        setPart(prev => ({ ...prev, ...patch }));
                    }}
                />
                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={addPart.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={addPart.isPending}>
                        {addPart.isPending ? <Trans>Adding…</Trans> : <Trans>Add part</Trans>}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}

export function AddRatePeriodModal({
    loanId,
    part,
    onClose,
}: {
    loanId: LoanId;
    part: LoanPart;
    onClose: () => void;
}) {
    const { t } = useLingui();
    const addRatePeriod = useAddRatePeriod();
    const toast = useToast();
    const [effectiveDate, setEffectiveDate] = useState(todayIso());
    const [ratePercent, setRatePercent] = useState('');
    const [fixedUntil, setFixedUntil] = useState('');
    const [topError, setTopError] = useState<string | null>(null);

    async function submit() {
        setTopError(null);
        const rate = Number(ratePercent.trim());
        if (ratePercent.trim() === '' || !Number.isFinite(rate) || rate < 0) {
            setTopError(t`Enter a valid annual rate (e.g. 3.8).`);
            return;
        }
        try {
            await addRatePeriod.mutateAsync({
                id: loanId,
                partId: part.id,
                request: {
                    effectiveDate,
                    annualRatePercent: rate,
                    fixedUntil: fixedUntil === '' ? null : fixedUntil,
                },
            });
            toast.success(t`Rate period added.`);
            onClose();
        } catch (err) {
            handleFormError(err, {
                setFieldErrors: () => undefined,
                setTopError,
                toast: toast.error,
            });
        }
    }

    return (
        <Modal open onClose={onClose} title={t`New rate period - ${part.label}`} width="sm">
            <Form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <div className="flex flex-col gap-3">
                    <DatePicker
                        label={t`Effective date`}
                        value={effectiveDate}
                        onChange={setEffectiveDate}
                    />
                    <TextField
                        label={t`Annual rate (%)`}
                        value={ratePercent}
                        onChange={setRatePercent}
                        isRequired
                        autoFocus
                        placeholder={t`e.g. 4.1`}
                        inputClassName="tabular-nums"
                    />
                    <DatePicker
                        label={t`Fixed until (optional)`}
                        value={fixedUntil}
                        onChange={setFixedUntil}
                    />
                    <span className="text-xs text-fg-3">
                        <Trans>
                            A future effective date records an accepted renewal offer. Existing
                            rates can be edited or removed from the part&apos;s rate history.
                        </Trans>
                    </span>
                </div>
                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={addRatePeriod.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={addRatePeriod.isPending}>
                        {addRatePeriod.isPending ? (
                            <Trans>Adding…</Trans>
                        ) : (
                            <Trans>Add rate period</Trans>
                        )}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}

/** Edit the loan-level terms: name, lender, interest account, and the Construction deposit. */
export function EditLoanModal({ loan, onClose }: { loan: LoanDetail; onClose: () => void }) {
    const { t } = useLingui();
    const update = useUpdateLoan();
    const toast = useToast();
    const counterparties = useCounterparties();

    const [name, setName] = useState(loan.name);
    const [lenderId, setLenderId] = useState<CounterpartyId | null>(loan.lenderCounterpartyId);
    const [interestAccountId, setInterestAccountId] = useState<AccountId | null>(
        loan.interestExpenseAccountId,
    );
    const [deposit, setDeposit] = useState<DepositDraft>(() =>
        loan.constructionDeposit
            ? {
                  accountId: loan.constructionDeposit.accountId,
                  incomeAccountId: loan.constructionDeposit.interestIncomeAccountId,
                  ratePercent: String(loan.constructionDeposit.annualRatePercent),
              }
            : emptyDeposit(),
    );
    const [topError, setTopError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | null>(null);

    const lenderItems = useMemo(
        () =>
            [...(counterparties.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name))
                .map(c => ({ key: c.id, label: c.name, value: c.id })),
        [counterparties.data],
    );

    async function submit() {
        setTopError(null);
        setFieldErrors(null);
        if (name.trim() === '') {
            setTopError(t`The loan needs a name.`);
            return;
        }
        if (lenderId === null) {
            setTopError(t`Pick the lender.`);
            return;
        }
        if (interestAccountId === null) {
            setTopError(t`Pick the interest expense account.`);
            return;
        }
        const depositResult = buildDeposit(deposit);
        if (!depositResult.ok) {
            setTopError(depositResult.error);
            return;
        }

        const original: UpdateLoanInput = {
            name: loan.name,
            lenderCounterpartyId: loan.lenderCounterpartyId,
            interestExpenseAccountId: loan.interestExpenseAccountId,
            constructionDepositAccountId: loan.constructionDeposit?.accountId ?? null,
            constructionDepositInterestIncomeAccountId:
                loan.constructionDeposit?.interestIncomeAccountId ?? null,
            constructionDepositAnnualRatePercent:
                loan.constructionDeposit?.annualRatePercent ?? null,
        };
        const edited: UpdateLoanInput = {
            name: name.trim(),
            lenderCounterpartyId: lenderId,
            interestExpenseAccountId: interestAccountId,
            ...depositResult.value,
        };

        try {
            await update.mutateAsync({ id: loan.id, original, edited });
            toast.success(t`Loan updated.`);
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal open onClose={onClose} title={t`Edit loan`} width="lg">
            <Form
                validationErrors={fieldErrors ?? undefined}
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <div className="grid grid-cols-2 gap-3 mb-3">
                    <TextField
                        label={t`Name`}
                        name="Name"
                        value={name}
                        onChange={setName}
                        isRequired
                        maxLength={128}
                        autoFocus
                    />
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            <Trans>Lender</Trans>
                        </span>
                        <ComboBox
                            items={lenderItems}
                            value={lenderId}
                            onChange={setLenderId}
                            placeholder={t`Pick the lender…`}
                            ariaLabel={t`Lender`}
                        />
                    </div>
                </div>
                <div className="flex flex-col gap-1 mb-1">
                    <span className="text-xs font-medium text-fg-2">
                        <Trans>Interest expense account</Trans>
                    </span>
                    <AccountSelect
                        value={interestAccountId}
                        onChange={setInterestAccountId}
                        postableOnly
                        type="Expense"
                        placeholder={t`e.g. Mortgage interest`}
                        ariaLabel={t`Interest expense account`}
                    />
                    <FieldError name="InterestExpenseAccountId" errors={fieldErrors} />
                </div>

                <ConstructionDepositFields
                    value={deposit}
                    currencyCode={loan.currencyCode}
                    onChange={setDeposit}
                />

                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={update.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={update.isPending}>
                        {update.isPending ? <Trans>Saving…</Trans> : <Trans>Save changes</Trans>}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}

/** Edit a part's terms. The part's account is immutable (ADR-0025). */
export function EditLoanPartModal({
    loanId,
    part,
    onClose,
}: {
    loanId: LoanId;
    part: LoanPart;
    onClose: () => void;
}) {
    const { t, i18n } = useLingui();
    const update = useUpdateLoanPart();
    const toast = useToast();
    const [label, setLabel] = useState(part.label);
    const [repaymentType, setRepaymentType] = useState<RepaymentType>(part.repaymentType);
    const [startDate, setStartDate] = useState(part.startDate);
    const [endDate, setEndDate] = useState(part.endDate);
    const [topError, setTopError] = useState<string | null>(null);

    async function submit() {
        setTopError(null);
        if (label.trim() === '') {
            setTopError(t`A part needs a label.`);
            return;
        }
        if (endDate === '' || endDate <= startDate) {
            setTopError(t`End date must be after the start date.`);
            return;
        }
        try {
            await update.mutateAsync({
                id: loanId,
                partId: part.id,
                request: { label: label.trim(), repaymentType, startDate, endDate },
            });
            toast.success(t`Part updated.`);
            onClose();
        } catch (err) {
            handleFormError(err, {
                setFieldErrors: () => undefined,
                setTopError,
                toast: toast.error,
            });
        }
    }

    return (
        <Modal open onClose={onClose} title={t`Edit part - ${part.label}`} width="sm">
            <Form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <div className="flex flex-col gap-3">
                    <TextField
                        label={t`Label`}
                        value={label}
                        onChange={setLabel}
                        isRequired
                        autoFocus
                        maxLength={64}
                    />
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            <Trans>Repayment type</Trans>
                        </span>
                        <Select
                            aria-label={t`Repayment type`}
                            value={repaymentType}
                            onChange={key => {
                                if (key !== null) setRepaymentType(key as RepaymentType);
                            }}
                        >
                            {REPAYMENT_TYPES.map(rt => (
                                <SelectItem key={rt.id} id={rt.id} textValue={i18n._(rt.label)}>
                                    {i18n._(rt.label)}
                                </SelectItem>
                            ))}
                        </Select>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                        <DatePicker
                            label={t`Start date`}
                            value={startDate}
                            onChange={setStartDate}
                        />
                        <DatePicker label={t`End date`} value={endDate} onChange={setEndDate} />
                    </div>
                    <span className="text-xs text-fg-3">
                        <Trans>
                            The part&apos;s account holds its principal and posted history, so it
                            can&apos;t be changed here.
                        </Trans>
                    </span>
                </div>
                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={update.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={update.isPending}>
                        {update.isPending ? <Trans>Saving…</Trans> : <Trans>Save changes</Trans>}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}

/** Edit an existing rate period (ADR-0026: corrections are safe — the projection is computed). */
export function EditRatePeriodModal({
    loanId,
    partId,
    rate,
    onClose,
}: {
    loanId: LoanId;
    partId: LoanPartId;
    rate: LoanRatePeriod;
    onClose: () => void;
}) {
    const { t } = useLingui();
    const update = useUpdateRatePeriod();
    const toast = useToast();
    const [effectiveDate, setEffectiveDate] = useState(rate.effectiveDate);
    const [ratePercent, setRatePercent] = useState(String(rate.annualRatePercent));
    const [fixedUntil, setFixedUntil] = useState(rate.fixedUntil ?? '');
    const [topError, setTopError] = useState<string | null>(null);

    async function submit() {
        setTopError(null);
        const parsed = Number(ratePercent.trim());
        if (ratePercent.trim() === '' || !Number.isFinite(parsed) || parsed < 0) {
            setTopError(t`Enter a valid annual rate (e.g. 3.8).`);
            return;
        }
        try {
            await update.mutateAsync({
                id: loanId,
                partId,
                ratePeriodId: rate.id,
                request: {
                    effectiveDate,
                    annualRatePercent: parsed,
                    fixedUntil: fixedUntil === '' ? null : fixedUntil,
                },
            });
            toast.success(t`Rate period updated.`);
            onClose();
        } catch (err) {
            handleFormError(err, {
                setFieldErrors: () => undefined,
                setTopError,
                toast: toast.error,
            });
        }
    }

    return (
        <Modal open onClose={onClose} title={t`Edit rate period`} width="sm">
            <Form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <div className="flex flex-col gap-3">
                    <DatePicker
                        label={t`Effective date`}
                        value={effectiveDate}
                        onChange={setEffectiveDate}
                    />
                    <TextField
                        label={t`Annual rate (%)`}
                        value={ratePercent}
                        onChange={setRatePercent}
                        isRequired
                        autoFocus
                        placeholder={t`e.g. 4.1`}
                        inputClassName="tabular-nums"
                    />
                    <DatePicker
                        label={t`Fixed until (optional)`}
                        value={fixedUntil}
                        onChange={setFixedUntil}
                    />
                </div>
                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={update.isPending}>
                        <Trans>Cancel</Trans>
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={update.isPending}>
                        {update.isPending ? <Trans>Saving…</Trans> : <Trans>Save changes</Trans>}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}
