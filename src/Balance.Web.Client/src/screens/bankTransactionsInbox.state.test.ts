import { describe, expect, it, vi } from 'vitest';
import type { Account } from '../api/accounts';
import type { BankAccount } from '../api/bankAccounts';
import type { BankTransaction } from '../api/bankTransactions';
import {
    asAccountId,
    asBankAccountId,
    asBankTransactionId,
    asCounterpartyId,
    type AccountId,
    type BankTransactionId,
    type CounterpartyId,
} from '../lib/domain';
import {
    allVisibleSelectionState,
    applyBulkPatchToOverride,
    buildRowRequest,
    clearVisibleSelection,
    collectNewCounterpartyNames,
    computeRangeSelection,
    distinctRowCurrencies,
    emptyDraft,
    initialPrefill,
    isPristine,
    pickSuggestedAccountId,
    resolveCounterpartyByIban,
    rowStatus,
    runSaveAll,
    selectAllVisible,
    toggleSelection,
    withSuggestedAccount,
    type RowDraft,
    type SaveAllRow,
} from './bankTransactionsInbox.state';

const cpA = asCounterpartyId('11111111-1111-1111-1111-111111111111');
const cpB = asCounterpartyId('22222222-2222-2222-2222-222222222222');
const cpC = asCounterpartyId('33333333-3333-3333-3333-333333333333');

const accGroceries = asAccountId('a1111111-1111-1111-1111-111111111111');
const accCurrent = asAccountId('a2222222-2222-2222-2222-222222222222');
const accSavings = asAccountId('a3333333-3333-3333-3333-333333333333');
const accUsdInbox = asAccountId('a4444444-4444-4444-4444-444444444444');

const baCurrent = asBankAccountId('b1111111-1111-1111-1111-111111111111');
const baExternal = asBankAccountId('b2222222-2222-2222-2222-222222222222');

function bt(overrides: Partial<BankTransaction> = {}): BankTransaction {
    return {
        id: asBankTransactionId('bt000001-0000-0000-0000-000000000000'),
        bankAccountId: baCurrent,
        bookingDate: '2026-05-20',
        money: { amount: -4200, currencyCode: 'EUR' },
        description: 'AH groceries',
        counterpartyName: 'AH',
        counterpartyAccountNumber: 'NL00BANK1234567890',
        journalEntryId: null,
        dismissedAt: null,
        dismissedReason: null,
        ...overrides,
    };
}

function account(id: AccountId, overrides: Partial<Account> = {}): Account {
    return {
        id,
        name: 'Account',
        type: 'Expense',
        currencyCode: 'EUR',
        balance: { amount: 0, currencyCode: 'EUR' },
        bankAccount: null,
        ...overrides,
    };
}

/** Vitest's `toBeNull` doesn't narrow — this guard does, so the rest of the
 *  test reads `req.x` instead of `req!.x` and stays inside the no-non-null
 *  assertion lint rule. */
function expectNotNull<T>(value: T | null): T {
    expect(value).not.toBeNull();
    if (value === null) throw new Error('expected non-null value');
    return value;
}

describe('resolveCounterpartyByIban', () => {
    it('returns the CounterpartyId linked to a matching BankAccount IBAN (case/spaces insensitive)', () => {
        const bankAccounts: BankAccount[] = [
            {
                id: baExternal,
                iban: 'nl00 bank 1234 5678 90',
                accountNumber: null,
                bic: null,
                bankName: null,
                accountHolderName: null,
                currencyCode: 'EUR',
                accountId: null,
                counterpartyId: cpA,
            },
        ];
        expect(resolveCounterpartyByIban('NL00BANK1234567890', bankAccounts)).toBe(cpA);
    });

    it('returns null when no IBAN is supplied', () => {
        expect(resolveCounterpartyByIban(null, [])).toBeNull();
    });
});

describe('rowStatus', () => {
    it('returns empty when nothing is set', () => {
        expect(rowStatus(emptyDraft())).toBe('empty');
    });

    it('returns ready when an existing counterparty and account are set', () => {
        expect(
            rowStatus({
                counterpartyMode: 'existing',
                counterpartyId: cpA,
                newCounterpartyName: '',
                accountId: accGroceries,
            }),
        ).toBe('ready');
    });

    it('returns ready for a self-transfer (existing mode, null cp, account set)', () => {
        expect(
            rowStatus({
                counterpartyMode: 'existing',
                counterpartyId: null,
                newCounterpartyName: '',
                accountId: accSavings,
            }),
        ).toBe('ready');
    });

    it('returns ready for a new-counterparty row with an account', () => {
        expect(
            rowStatus({
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'New Vendor',
                accountId: accGroceries,
            }),
        ).toBe('ready');
    });

    it('returns invalid when the counterparty side is set but no account', () => {
        expect(
            rowStatus({
                counterpartyMode: 'existing',
                counterpartyId: cpA,
                newCounterpartyName: '',
                accountId: null,
            }),
        ).toBe('invalid');
    });

    it('returns invalid when an account is set but new-counterparty name is blank', () => {
        expect(
            rowStatus({
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: '   ',
                accountId: accGroceries,
            }),
        ).toBe('invalid');
    });
});

describe('pickSuggestedAccountId', () => {
    it('returns the first suggestion that matches currency and is not the BT bank-side account', () => {
        const accountsById = new Map<AccountId, Account>([
            [accCurrent, account(accCurrent, { currencyCode: 'EUR' })],
            [accGroceries, account(accGroceries, { currencyCode: 'EUR' })],
            [accUsdInbox, account(accUsdInbox, { currencyCode: 'USD' })],
        ]);
        const id = pickSuggestedAccountId(
            [
                { accountId: accCurrent, amount: -4200 },
                { accountId: accUsdInbox, amount: -4200 },
                { accountId: accGroceries, amount: -4200 },
            ],
            accountsById,
            'EUR',
            accCurrent,
        );
        expect(id).toBe(accGroceries);
    });

    it('returns null when no suggestion matches', () => {
        expect(pickSuggestedAccountId([], new Map(), 'EUR', null)).toBeNull();
    });
});

describe('withSuggestedAccount', () => {
    it('fills the account when prefill has none', () => {
        const updated = withSuggestedAccount(emptyDraft(), accGroceries);
        expect(updated.accountId).toBe(accGroceries);
    });

    it('does not overwrite an existing account', () => {
        const start: RowDraft = { ...emptyDraft(), accountId: accCurrent };
        const updated = withSuggestedAccount(start, accGroceries);
        expect(updated.accountId).toBe(accCurrent);
    });
});

describe('collectNewCounterpartyNames', () => {
    it('returns each distinct (case-insensitive) name once, ignoring blank rows and existing-mode rows', () => {
        const rows = [
            { draft: { ...emptyDraft(), counterpartyMode: 'new', newCounterpartyName: 'Acme Co' } },
            { draft: { ...emptyDraft(), counterpartyMode: 'new', newCounterpartyName: 'ACME CO' } },
            { draft: { ...emptyDraft(), counterpartyMode: 'new', newCounterpartyName: '   ' } },
            { draft: { ...emptyDraft(), counterpartyMode: 'new', newCounterpartyName: 'Beta' } },
            {
                draft: {
                    ...emptyDraft(),
                    counterpartyMode: 'existing',
                    counterpartyId: cpA,
                    newCounterpartyName: 'Ignored',
                },
            },
        ] as const;
        expect(collectNewCounterpartyNames(rows)).toEqual(['Acme Co', 'Beta']);
    });
});

describe('initialPrefill', () => {
    it('seeds counterpartyId from an IBAN match against BankAccounts', () => {
        const bankAccounts: BankAccount[] = [
            {
                id: baExternal,
                iban: 'NL00BANK1234567890',
                accountNumber: null,
                bic: null,
                bankName: null,
                accountHolderName: null,
                currencyCode: 'EUR',
                accountId: null,
                counterpartyId: cpB,
            },
        ];
        const prefill = initialPrefill(bt(), bankAccounts);
        expect(prefill).toEqual({
            counterpartyMode: 'existing',
            counterpartyId: cpB,
            newCounterpartyName: '',
            accountId: null,
        });
    });
});

describe('isPristine', () => {
    it('matches identical drafts', () => {
        const a = emptyDraft();
        expect(isPristine(a, a)).toBe(true);
    });

    it('flags account changes', () => {
        const prefill = emptyDraft();
        expect(isPristine({ ...prefill, accountId: accGroceries }, prefill)).toBe(false);
    });
});

describe('buildRowRequest', () => {
    it('projects a ready row to a single-line categorise request with the inverse sign', () => {
        const row = bt({ money: { amount: -4200, currencyCode: 'EUR' } });
        const draft: RowDraft = {
            counterpartyMode: 'existing',
            counterpartyId: cpA,
            newCounterpartyName: '',
            accountId: accGroceries,
        };
        const req = expectNotNull(buildRowRequest(row, draft, new Map()));
        expect(req.counterpartyId).toBe(cpA);
        expect(req.newCounterparty).toBeNull();
        expect(req.date).toBe('2026-05-20');
        expect(req.description).toBe('AH groceries');
        expect(req.lines).toHaveLength(1);
        expect(req.lines[0]).toEqual({ accountId: accGroceries, amount: 4200, description: null });
    });

    it('uses a negative line amount for an incoming BT', () => {
        const row = bt({ money: { amount: 25_000, currencyCode: 'EUR' } });
        const draft: RowDraft = {
            counterpartyMode: 'existing',
            counterpartyId: cpA,
            newCounterpartyName: '',
            accountId: accGroceries,
        };
        const req = expectNotNull(buildRowRequest(row, draft, new Map()));
        expect(req.lines[0]?.amount).toBe(-25_000);
    });

    it('emits a null counterpartyId for a self-transfer', () => {
        const draft: RowDraft = {
            counterpartyMode: 'existing',
            counterpartyId: null,
            newCounterpartyName: '',
            accountId: accSavings,
        };
        const req = expectNotNull(buildRowRequest(bt(), draft, new Map()));
        expect(req.counterpartyId).toBeNull();
    });

    it('rewrites a new-CP row to its preflight-created counterpartyId', () => {
        const draft: RowDraft = {
            counterpartyMode: 'new',
            counterpartyId: null,
            newCounterpartyName: 'Acme Co',
            accountId: accGroceries,
        };
        const created = new Map<string, CounterpartyId>([['acme co', cpC]]);
        const req = expectNotNull(buildRowRequest(bt(), draft, created));
        expect(req.counterpartyId).toBe(cpC);
        expect(req.newCounterparty).toBeNull();
    });

    it('returns null for a not-ready row', () => {
        const draft: RowDraft = {
            counterpartyMode: 'existing',
            counterpartyId: cpA,
            newCounterpartyName: '',
            accountId: null,
        };
        expect(buildRowRequest(bt(), draft, new Map())).toBeNull();
    });
});

describe('runSaveAll', () => {
    function readyRow(
        id: string,
        draft: Partial<RowDraft> = {},
        btOverride: Partial<BankTransaction> = {},
    ): SaveAllRow {
        return {
            id: asBankTransactionId(id),
            bt: bt(btOverride),
            draft: {
                counterpartyMode: 'existing',
                counterpartyId: cpA,
                newCounterpartyName: '',
                accountId: accGroceries,
                ...draft,
            },
        };
    }

    it('runs categorise sequentially in row order', async () => {
        const order: BankTransactionId[] = [];
        let inFlight = 0;
        let maxInFlight = 0;
        const categorize = vi.fn(async (id: BankTransactionId) => {
            inFlight += 1;
            maxInFlight = Math.max(maxInFlight, inFlight);
            await Promise.resolve();
            order.push(id);
            inFlight -= 1;
        });
        const rows = [
            readyRow('bt000001-0000-0000-0000-000000000001'),
            readyRow('bt000001-0000-0000-0000-000000000002'),
            readyRow('bt000001-0000-0000-0000-000000000003'),
        ];
        const summary = await runSaveAll(rows, {
            createCounterparty: vi.fn(),
            categorize,
        });
        expect(summary).toEqual({ saved: 3, failed: 0 });
        expect(order).toEqual(rows.map(r => r.id));
        expect(maxInFlight).toBe(1);
    });

    it('preflights each distinct new counterparty name once, before any categorise call', async () => {
        const events: string[] = [];
        const createCounterparty = vi.fn((name: string) => {
            events.push(`create:${name}`);
            return Promise.resolve(name === 'Acme Co' ? cpB : cpC);
        });
        const categorize = vi.fn((id: BankTransactionId) => {
            events.push(`categorize:${id}`);
            return Promise.resolve();
        });
        const rows = [
            readyRow('bt000001-0000-0000-0000-00000000000a', {
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'Acme Co',
            }),
            readyRow('bt000001-0000-0000-0000-00000000000b', {
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'ACME CO',
            }),
            readyRow('bt000001-0000-0000-0000-00000000000c', {
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'Beta',
            }),
        ];

        const summary = await runSaveAll(rows, { createCounterparty, categorize });

        expect(summary.saved).toBe(3);
        expect(createCounterparty).toHaveBeenCalledTimes(2);
        expect(createCounterparty).toHaveBeenNthCalledWith(1, 'Acme Co');
        expect(createCounterparty).toHaveBeenNthCalledWith(2, 'Beta');
        // every create happens before any categorise
        const firstCategorize = events.findIndex(e => e.startsWith('categorize:'));
        const lastCreate = events.findLastIndex(e => e.startsWith('create:'));
        expect(firstCategorize).toBeGreaterThan(lastCreate);
    });

    it('continues past a failed categorise (best-effort, no abort) and reports the failure per row', async () => {
        const outcomes: { id: BankTransactionId; ok: boolean; error?: string }[] = [];
        let call = 0;
        const categorize = vi.fn(() => {
            call += 1;
            if (call === 2) return Promise.reject(new Error('boom'));
            return Promise.resolve();
        });
        const rows = [
            readyRow('bt000001-0000-0000-0000-000000000011'),
            readyRow('bt000001-0000-0000-0000-000000000012'),
            readyRow('bt000001-0000-0000-0000-000000000013'),
        ];
        const summary = await runSaveAll(rows, {
            createCounterparty: vi.fn(),
            categorize,
            onRowResult: (id, outcome) =>
                outcomes.push(
                    outcome.ok
                        ? { id, ok: true }
                        : { id, ok: false, error: outcome.error },
                ),
        });
        expect(summary).toEqual({ saved: 2, failed: 1 });
        expect(categorize).toHaveBeenCalledTimes(3);
        expect(outcomes).toEqual([
            { id: rows[0]?.id, ok: true },
            { id: rows[1]?.id, ok: false, error: 'boom' },
            { id: rows[2]?.id, ok: true },
        ]);
    });

    it('fails every row whose new-CP creation failed, but still saves the others', async () => {
        const createCounterparty = vi.fn((name: string) => {
            if (name === 'Bad') return Promise.reject(new Error('cp boom'));
            return Promise.resolve(cpB);
        });
        const categorize = vi.fn(() => Promise.resolve());
        const rows = [
            readyRow('bt000001-0000-0000-0000-000000000021', {
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'Bad',
            }),
            readyRow('bt000001-0000-0000-0000-000000000022', {
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'BAD',
            }),
            readyRow('bt000001-0000-0000-0000-000000000023', {
                counterpartyMode: 'new',
                counterpartyId: null,
                newCounterpartyName: 'Good',
            }),
        ];
        const failures: { id: BankTransactionId; error: string }[] = [];
        const summary = await runSaveAll(rows, {
            createCounterparty,
            categorize,
            onRowResult: (id, outcome) => {
                if (!outcome.ok) failures.push({ id, error: outcome.error });
            },
        });
        expect(summary).toEqual({ saved: 1, failed: 2 });
        expect(categorize).toHaveBeenCalledTimes(1);
        expect(failures.map(f => f.id)).toEqual([rows[0]?.id, rows[1]?.id]);
    });

    it('emits onProgress with monotonically increasing done counts', async () => {
        const progress: [number, number][] = [];
        const rows = [
            readyRow('bt000001-0000-0000-0000-000000000031'),
            readyRow('bt000001-0000-0000-0000-000000000032'),
        ];
        await runSaveAll(rows, {
            createCounterparty: vi.fn(),
            categorize: vi.fn(() => Promise.resolve()),
            onProgress: (done, total) => progress.push([done, total]),
        });
        expect(progress).toEqual([
            [0, 2],
            [1, 2],
            [2, 2],
        ]);
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// Multi-select + bulk-apply (issue #85)
// ─────────────────────────────────────────────────────────────────────────────

const id1 = asBankTransactionId('bt000001-0000-0000-0000-000000000101');
const id2 = asBankTransactionId('bt000001-0000-0000-0000-000000000102');
const id3 = asBankTransactionId('bt000001-0000-0000-0000-000000000103');
const id4 = asBankTransactionId('bt000001-0000-0000-0000-000000000104');
const id5 = asBankTransactionId('bt000001-0000-0000-0000-000000000105');

describe('toggleSelection', () => {
    it('adds an id when missing', () => {
        const next = toggleSelection(new Set(), id1);
        expect(next.has(id1)).toBe(true);
    });

    it('removes an id when present', () => {
        const next = toggleSelection(new Set([id1, id2]), id1);
        expect(next.has(id1)).toBe(false);
        expect(next.has(id2)).toBe(true);
    });

    it('returns a new set (does not mutate input)', () => {
        const start = new Set([id1]);
        const next = toggleSelection(start, id2);
        expect(start.has(id2)).toBe(false);
        expect(next).not.toBe(start);
    });
});

describe('computeRangeSelection', () => {
    const ordered = [id1, id2, id3, id4, id5];

    it('selects every id in the range when the target was unselected (anchor before target)', () => {
        const next = computeRangeSelection(ordered, new Set(), id2, id4);
        expect([...next].sort()).toEqual([id2, id3, id4].sort());
    });

    it('selects every id in the range when anchor is after target', () => {
        const next = computeRangeSelection(ordered, new Set(), id4, id2);
        expect([...next].sort()).toEqual([id2, id3, id4].sort());
    });

    it('deselects every id in the range when the target was already selected', () => {
        const next = computeRangeSelection(
            ordered,
            new Set([id1, id2, id3, id4]),
            id2,
            id4,
        );
        expect(next.has(id1)).toBe(true);
        expect(next.has(id2)).toBe(false);
        expect(next.has(id3)).toBe(false);
        expect(next.has(id4)).toBe(false);
    });

    it('preserves selection state of ids outside the range', () => {
        const next = computeRangeSelection(ordered, new Set([id5]), id1, id2);
        expect(next.has(id5)).toBe(true);
        expect(next.has(id1)).toBe(true);
        expect(next.has(id2)).toBe(true);
    });

    it('falls back to a single toggle when the anchor is no longer visible', () => {
        const ghost = asBankTransactionId('bt000001-0000-0000-0000-0000000000ff');
        const next = computeRangeSelection(ordered, new Set(), ghost, id3);
        expect([...next]).toEqual([id3]);
    });
});

describe('selectAllVisible / clearVisibleSelection', () => {
    it('selectAllVisible unions the visible ids into the selection', () => {
        const next = selectAllVisible(new Set([id5]), [id1, id2]);
        expect(next.has(id1)).toBe(true);
        expect(next.has(id2)).toBe(true);
        expect(next.has(id5)).toBe(true);
    });

    it('clearVisibleSelection drops visible ids but leaves others', () => {
        const next = clearVisibleSelection(new Set([id1, id2, id5]), [id1, id2]);
        expect(next.has(id1)).toBe(false);
        expect(next.has(id2)).toBe(false);
        expect(next.has(id5)).toBe(true);
    });
});

describe('allVisibleSelectionState', () => {
    it("returns 'all' when every visible id is selected", () => {
        expect(allVisibleSelectionState(new Set([id1, id2]), [id1, id2])).toBe('all');
    });

    it("returns 'none' when no visible id is selected", () => {
        expect(allVisibleSelectionState(new Set(), [id1, id2])).toBe('none');
    });

    it("returns 'some' when only part of the visible set is selected", () => {
        expect(allVisibleSelectionState(new Set([id1]), [id1, id2])).toBe('some');
    });

    it("returns 'none' for an empty visible list", () => {
        expect(allVisibleSelectionState(new Set([id1]), [])).toBe('none');
    });
});

describe('distinctRowCurrencies', () => {
    function row(currency: string) {
        return { money: { currencyCode: currency } };
    }

    it('returns the distinct currency set in first-seen order', () => {
        const cs = distinctRowCurrencies([
            row('EUR'),
            row('EUR'),
            row('USD'),
            row('GBP'),
        ]);
        expect(cs).toEqual(['EUR', 'USD', 'GBP']);
    });

    it('returns an empty list for no rows', () => {
        expect(distinctRowCurrencies([])).toEqual([]);
    });
});

describe('applyBulkPatchToOverride', () => {
    it('writes both fields when both pickers are filled, overwriting prior edits', () => {
        const prior: Partial<RowDraft> = {
            counterpartyMode: 'existing',
            counterpartyId: cpA,
            accountId: accCurrent,
        };
        const out = applyBulkPatchToOverride(prior, {
            counterparty: { kind: 'existing', counterpartyId: cpB },
            accountId: accGroceries,
        });
        expect(out).toEqual({
            counterpartyMode: 'existing',
            counterpartyId: cpB,
            newCounterpartyName: '',
            accountId: accGroceries,
        });
    });

    it('leaves the account untouched when only Counterparty is supplied', () => {
        const prior: Partial<RowDraft> = { accountId: accCurrent };
        const out = applyBulkPatchToOverride(prior, {
            counterparty: { kind: 'existing', counterpartyId: cpA },
            accountId: null,
        });
        expect(out.accountId).toBe(accCurrent);
        expect(out.counterpartyMode).toBe('existing');
        expect(out.counterpartyId).toBe(cpA);
    });

    it('leaves the counterparty untouched when only Account is supplied', () => {
        const prior: Partial<RowDraft> = {
            counterpartyMode: 'existing',
            counterpartyId: cpA,
        };
        const out = applyBulkPatchToOverride(prior, {
            counterparty: null,
            accountId: accGroceries,
        });
        expect(out.counterpartyMode).toBe('existing');
        expect(out.counterpartyId).toBe(cpA);
        expect(out.accountId).toBe(accGroceries);
    });

    it('applies a self-transfer (existing mode, null cpId) when the picker says so', () => {
        const out = applyBulkPatchToOverride(undefined, {
            counterparty: { kind: 'existing', counterpartyId: null },
            accountId: accSavings,
        });
        expect(out.counterpartyMode).toBe('existing');
        expect(out.counterpartyId).toBeNull();
        expect(out.newCounterpartyName).toBe('');
        expect(out.accountId).toBe(accSavings);
    });

    it("applies a 'new' counterparty by name", () => {
        const out = applyBulkPatchToOverride(undefined, {
            counterparty: { kind: 'new', name: 'Acme Co' },
            accountId: null,
        });
        expect(out.counterpartyMode).toBe('new');
        expect(out.counterpartyId).toBeNull();
        expect(out.newCounterpartyName).toBe('Acme Co');
    });

    it('returns a fresh override (does not mutate the input)', () => {
        const prior: Partial<RowDraft> = { accountId: accCurrent };
        const out = applyBulkPatchToOverride(prior, {
            counterparty: null,
            accountId: accGroceries,
        });
        expect(out).not.toBe(prior);
        expect(prior.accountId).toBe(accCurrent);
    });
});
