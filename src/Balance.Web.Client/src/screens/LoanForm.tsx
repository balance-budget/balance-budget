import { useMemo, useState } from 'react';
import { Form } from 'react-aria-components';
import { useCounterparties } from '../api/counterparties';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useAddLoanPart,
    useAddRatePeriod,
    useCreateLoan,
    type LoanDetail,
    type LoanPart,
    type RepaymentType,
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
import { Select, SelectItem } from '../components/ui/Select';
import { TextField } from '../components/ui/TextField';
import { useToast } from '../components/ui/Toast';
import { todayIso } from '../lib/dates';
import type { AccountId, CounterpartyId, LoanId } from '../lib/domain';
import { handleFormError } from '../lib/formErrors';
import { parseMoney } from '../lib/money';

const REPAYMENT_TYPES: { id: RepaymentType; label: string }[] = [
    { id: 'Annuity', label: 'Annuity' },
    { id: 'Linear', label: 'Linear' },
    { id: 'InterestOnly', label: 'Interest-only' },
];

type PartDraft = {
    id: string;
    label: string;
    repaymentType: RepaymentType;
    startDate: string;
    endDate: string;
    mode: 'adopt' | 'new';
    adoptAccountId: AccountId | null;
    newName: string;
    newCode: string;
    openingBalance: string;
    ratePercent: string;
    fixedUntil: string;
};

let partCounter = 0;
function emptyPart(index: number): PartDraft {
    partCounter += 1;
    return {
        id: `part-${partCounter.toString(36)}`,
        label: `Part ${(index + 1).toString()}`,
        repaymentType: 'Annuity',
        startDate: todayIso(),
        endDate: '',
        mode: 'adopt',
        adoptAccountId: null,
        newName: '',
        newCode: '',
        openingBalance: '',
        ratePercent: '',
        fixedUntil: '',
    };
}

type BuildPartResult =
    | { ok: true; request: WireCreateLoanPartRequest }
    | { ok: false; error: string };

function buildPartRequest(part: PartDraft, scale: number): BuildPartResult {
    if (part.label.trim() === '') return { ok: false, error: 'Every part needs a label.' };

    if (part.endDate === '') return { ok: false, error: 'Every part needs an end date.' };

    const rate = Number(part.ratePercent.trim());
    if (part.ratePercent.trim() === '' || !Number.isFinite(rate) || rate < 0) {
        return { ok: false, error: 'Enter a valid annual rate (e.g. 3.8).' };
    }

    let adoptAccountId: AccountId | null = null;
    let newAccount: WireCreateLoanPartRequest['newAccount'] = null;
    if (part.mode === 'adopt') {
        if (part.adoptAccountId === null) {
            return { ok: false, error: 'Pick the liability account to adopt.' };
        }
        adoptAccountId = part.adoptAccountId;
    } else {
        if (part.newName.trim() === '' || part.newCode.trim() === '') {
            return { ok: false, error: 'A fresh part account needs a name and a code.' };
        }
        const opening = parseMoney(
            part.openingBalance.trim() === '' ? '0' : part.openingBalance,
            scale,
        );
        if (!opening.ok || opening.minor < 0) {
            return { ok: false, error: 'Enter a valid opening balance.' };
        }
        newAccount = {
            name: part.newName.trim(),
            code: part.newCode.trim(),
            openingBalance: opening.minor,
            openingDate: part.startDate,
        };
    }

    return {
        ok: true,
        request: {
            label: part.label.trim(),
            repaymentType: part.repaymentType,
            startDate: part.startDate,
            endDate: part.endDate,
            adoptAccountId,
            newAccount,
            ratePeriods: [
                {
                    effectiveDate: part.startDate,
                    annualRatePercent: rate,
                    fixedUntil: part.fixedUntil === '' ? null : part.fixedUntil,
                },
            ],
        },
    };
}

export function LoanFormModal({ onClose }: { onClose: () => void }) {
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
            setTopError('Pick the lender.');
            return;
        }
        if (interestAccountId === null) {
            setTopError('Pick the interest expense account.');
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

        try {
            await create.mutateAsync({
                name,
                lenderCounterpartyId: lenderId,
                interestExpenseAccountId: interestAccountId,
                currencyCode,
                parentAccountName: parentName.trim() === '' ? name.trim() : parentName.trim(),
                parentAccountCode: parentCode.trim(),
                parts: partRequests,
            });
            toast.success('Loan created.');
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal open onClose={onClose} title="New loan" width="lg">
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
                        label="Name"
                        name="Name"
                        value={name}
                        onChange={setName}
                        isRequired
                        maxLength={128}
                        autoFocus
                        placeholder="e.g. Mortgage Hoofdstraat 1"
                    />
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">Lender</span>
                        <ComboBox
                            items={lenderItems}
                            value={lenderId}
                            onChange={setLenderId}
                            placeholder="Pick the lender…"
                            ariaLabel="Lender"
                        />
                        <span className="text-xs text-fg-3">
                            Drives the Inbox&apos;s loan-payment hint.
                        </span>
                    </div>
                </div>

                <div className="grid grid-cols-2 gap-3 mb-3">
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">
                            Interest expense account
                        </span>
                        <AccountSelect
                            value={interestAccountId}
                            onChange={setInterestAccountId}
                            postableOnly
                            type="Expense"
                            placeholder="e.g. Mortgage interest"
                            ariaLabel="Interest expense account"
                        />
                        <FieldError name="InterestExpenseAccountId" errors={fieldErrors} />
                    </div>
                    <div className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-fg-2">Currency</span>
                        <Select
                            aria-label="Currency"
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
                        label="Parent account name"
                        name="ParentAccountName"
                        value={parentName}
                        onChange={setParentName}
                        maxLength={128}
                        placeholder="Defaults to the loan name"
                    />
                    <TextField
                        label="Parent account code"
                        name="ParentAccountCode"
                        value={parentCode}
                        onChange={setParentCode}
                        isRequired
                        maxLength={32}
                        placeholder="e.g. 2200"
                        inputClassName="tabular-nums"
                    />
                </div>

                <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-medium text-fg-1">Loan parts</span>
                    <Button
                        variant="ghost"
                        onPress={() => {
                            setParts(prev => [...prev, emptyPart(prev.length)]);
                        }}
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        Add part
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

                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={create.isPending}>
                        Cancel
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={create.isPending}>
                        {create.isPending ? 'Creating…' : 'Create loan'}
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
    return (
        <div className="rounded-lg border border-border-soft p-3 flex flex-col gap-3">
            <div className="grid grid-cols-[1fr_1fr_auto] gap-3 items-end">
                <TextField
                    label="Label"
                    value={part.label}
                    onChange={label => {
                        onChange({ label });
                    }}
                    isRequired
                    maxLength={64}
                    placeholder="As on the lender's statement"
                />
                <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-fg-2">Repayment type</span>
                    <Select
                        aria-label="Repayment type"
                        value={part.repaymentType}
                        onChange={key => {
                            if (key !== null) onChange({ repaymentType: key as RepaymentType });
                        }}
                    >
                        {REPAYMENT_TYPES.map(t => (
                            <SelectItem key={t.id} id={t.id}>
                                {t.label}
                            </SelectItem>
                        ))}
                    </Select>
                </div>
                {onRemove && (
                    <Button variant="ghost" onPress={onRemove} aria-label="Remove part">
                        <Icon name="trash" size={14} strokeWidth={2} />
                    </Button>
                )}
            </div>

            <div className="grid grid-cols-2 gap-3">
                <DatePicker
                    label="Start date"
                    value={part.startDate}
                    onChange={startDate => {
                        onChange({ startDate });
                    }}
                />
                <DatePicker
                    label="End date"
                    value={part.endDate}
                    onChange={endDate => {
                        onChange({ endDate });
                    }}
                />
            </div>

            <div className="grid grid-cols-2 gap-3">
                <TextField
                    label="Annual rate (%)"
                    value={part.ratePercent}
                    onChange={ratePercent => {
                        onChange({ ratePercent });
                    }}
                    isRequired
                    placeholder="e.g. 3.8"
                    inputClassName="tabular-nums"
                />
                <DatePicker
                    label="Rate fixed until (optional)"
                    value={part.fixedUntil}
                    onChange={fixedUntil => {
                        onChange({ fixedUntil });
                    }}
                />
            </div>

            <div className="flex items-center gap-4 text-sm">
                <label className="inline-flex items-center gap-1.5">
                    <input
                        type="radio"
                        checked={part.mode === 'adopt'}
                        onChange={() => {
                            onChange({ mode: 'adopt' });
                        }}
                    />
                    Adopt an existing account
                </label>
                <label className="inline-flex items-center gap-1.5">
                    <input
                        type="radio"
                        checked={part.mode === 'new'}
                        onChange={() => {
                            onChange({ mode: 'new' });
                        }}
                    />
                    Create a fresh account
                </label>
            </div>

            {part.mode === 'adopt' ? (
                <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-fg-2">Liability account</span>
                    <AccountSelect
                        value={part.adoptAccountId}
                        onChange={adoptAccountId => {
                            onChange({ adoptAccountId });
                        }}
                        postableOnly
                        type="Liability"
                        currencyCode={currencyCode}
                        placeholder="Existing mortgage / loan account…"
                        ariaLabel="Account to adopt"
                    />
                    <span className="text-xs text-fg-3">
                        Re-parented under the loan with its posted history intact.
                    </span>
                </div>
            ) : (
                <div className="grid grid-cols-3 gap-3">
                    <TextField
                        label="Account name"
                        value={part.newName}
                        onChange={newName => {
                            onChange({ newName });
                        }}
                        maxLength={128}
                    />
                    <TextField
                        label="Account code"
                        value={part.newCode}
                        onChange={newCode => {
                            onChange({ newCode });
                        }}
                        maxLength={32}
                        inputClassName="tabular-nums"
                    />
                    <TextField
                        label={`Opening balance (${currencyCode})`}
                        value={part.openingBalance}
                        onChange={openingBalance => {
                            onChange({ openingBalance });
                        }}
                        placeholder="Outstanding principal"
                        inputClassName="tabular-nums"
                    />
                </div>
            )}
        </div>
    );
}

export function AddLoanPartModal({ loan, onClose }: { loan: LoanDetail; onClose: () => void }) {
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
            toast.success('Part added.');
            onClose();
        } catch (err) {
            handleFormError(err, { setFieldErrors, setTopError, toast: toast.error });
        }
    }

    return (
        <Modal open onClose={onClose} title="Add loan part" width="lg">
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
                        Cancel
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={addPart.isPending}>
                        {addPart.isPending ? 'Adding…' : 'Add part'}
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
            setTopError('Enter a valid annual rate (e.g. 3.8).');
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
            toast.success('Rate period added.');
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
        <Modal open onClose={onClose} title={`New rate period — ${part.label}`} width="sm">
            <Form
                onSubmit={e => {
                    e.preventDefault();
                    void submit();
                }}
            >
                <FormErrorBanner message={topError} />
                <div className="flex flex-col gap-3">
                    <DatePicker
                        label="Effective date"
                        value={effectiveDate}
                        onChange={setEffectiveDate}
                    />
                    <TextField
                        label="Annual rate (%)"
                        value={ratePercent}
                        onChange={setRatePercent}
                        isRequired
                        autoFocus
                        placeholder="e.g. 4.1"
                        inputClassName="tabular-nums"
                    />
                    <DatePicker
                        label="Fixed until (optional)"
                        value={fixedUntil}
                        onChange={setFixedUntil}
                    />
                    <span className="text-xs text-fg-3">
                        Rate history is appended, never overwritten. A future effective date records
                        an accepted renewal offer.
                    </span>
                </div>
                <ModalFooter>
                    <Button variant="ghost" onPress={onClose} isDisabled={addRatePeriod.isPending}>
                        Cancel
                    </Button>
                    <Button type="submit" variant="primary" isDisabled={addRatePeriod.isPending}>
                        {addRatePeriod.isPending ? 'Adding…' : 'Add rate period'}
                    </Button>
                </ModalFooter>
            </Form>
        </Modal>
    );
}
