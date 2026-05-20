import type { Money } from '../api/accounts';

type Polarity = 'higher-is-good' | 'lower-is-good';

type Props = {
    current: Money;
    prior: Money;
    polarity: Polarity;
};

// MTD delta vs the same period last month (SPLM). Returns null when the prior
// period total is zero — no signal yet for brand-new accounts. Sign semantics:
// for expenses both `current` and `prior` are negative (money out), so the
// arithmetic ratio `(current - prior) / prior` naturally tracks magnitude
// direction in either polarity.
export function MtdDeltaChip({ current, prior, polarity }: Props) {
    if (prior.amount === 0) return null;

    const percent = Math.round(((current.amount - prior.amount) / prior.amount) * 100);
    const direction = percent === 0 ? 'flat' : percent > 0 ? 'up' : 'down';

    const isGood =
        (polarity === 'higher-is-good' && direction === 'up') ||
        (polarity === 'lower-is-good' && direction === 'down');

    const arrow = direction === 'up' ? '▲' : direction === 'down' ? '▼' : '—';
    const colorClass =
        direction === 'flat'
            ? 'text-fg-3 bg-surface-2'
            : isGood
                ? 'text-success bg-success-soft'
                : 'text-danger bg-danger-soft';

    return (
        <span
            className={[
                'inline-flex items-center gap-[3px] px-2 py-[2px] rounded-full text-[11px] font-medium leading-none whitespace-nowrap',
                colorClass,
            ].join(' ')}
        >
            <span className="text-[9px]">{arrow}</span>
            <span>{Math.abs(percent)}% vs Last month</span>
        </span>
    );
}
