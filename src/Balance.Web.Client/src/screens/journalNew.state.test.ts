import { describe, expect, it } from 'vitest';
import { asAccountId } from '../lib/domain';
import {
    emptyAdvancedLine,
    emptySimpleLeg,
    simpleToAdvanced,
    tryAdvancedToSimple,
    type AdvancedLine,
} from './journalNew.state';

const A = asAccountId('11111111-1111-1111-1111-111111111111');
const B = asAccountId('22222222-2222-2222-2222-222222222222');

function line(partial: Partial<AdvancedLine> = {}): AdvancedLine {
    return { ...emptyAdvancedLine(), ...partial };
}

describe('tryAdvancedToSimple', () => {
    it('returns canonical empty Simple state when all Advanced rows are empty (fresh form)', () => {
        const result = tryAdvancedToSimple([emptyAdvancedLine(), emptyAdvancedLine()]);
        expect(result).not.toBeNull();
        expect(result?.from).toHaveLength(1);
        expect(result?.to).toHaveLength(1);
        expect(result?.from[0]?.amount).toBe('');
        expect(result?.to[0]?.amount).toBe('');
    });

    it('skips account-only (no amount) rows', () => {
        const rows = [line({ accountId: A }), line({ accountId: B, debit: '100' })];
        const result = tryAdvancedToSimple(rows);
        expect(result).not.toBeNull();
        expect(result?.to).toHaveLength(1);
        expect(result?.to[0]?.amount).toBe('100');
        expect(result?.from).toHaveLength(1);
        expect(result?.from[0]?.amount).toBe('');
    });

    it('returns null when any row has both debit and credit set (genuine ambiguity)', () => {
        const rows = [
            line({ accountId: A, debit: '50', credit: '50' }),
            line({ accountId: B, debit: '100' }),
        ];
        expect(tryAdvancedToSimple(rows)).toBeNull();
    });

    it('round-trips a single debit row into a single To leg', () => {
        const rows = [line({ accountId: A, debit: '42' })];
        const result = tryAdvancedToSimple(rows);
        expect(result).not.toBeNull();
        expect(result?.to).toHaveLength(1);
        expect(result?.to[0]?.amount).toBe('42');
        expect(result?.to[0]?.accountId).toBe(A);
    });

    it('round-trips a single credit row into a single From leg', () => {
        const rows = [line({ accountId: A, credit: '42' })];
        const result = tryAdvancedToSimple(rows);
        expect(result).not.toBeNull();
        expect(result?.from).toHaveLength(1);
        expect(result?.from[0]?.amount).toBe('42');
    });

    it('round-trips Simple->Advanced->Simple unchanged for a fresh form', () => {
        const fresh = {
            from: [emptySimpleLeg()],
            to: [emptySimpleLeg()],
        };
        const advanced = simpleToAdvanced(fresh);
        const back = tryAdvancedToSimple(advanced);
        expect(back).not.toBeNull();
        expect(back?.from).toHaveLength(1);
        expect(back?.to).toHaveLength(1);
    });
});

describe('emptyAdvancedLine and emptySimpleLeg', () => {
    it('produce unique ids', () => {
        const a = emptyAdvancedLine();
        const b = emptyAdvancedLine();
        expect(a.id).not.toBe(b.id);
        const c = emptySimpleLeg();
        const d = emptySimpleLeg();
        expect(c.id).not.toBe(d.id);
    });
});
