import { Trans } from '@lingui/react/macro';
import type { Money } from '../api/accounts';
import { cx } from '../lib/cx';

type Polarity = 'higher-is-good' | 'lower-is-good';

type Props = {
    current: Money;
    prior: Money;
    polarity: Polarity;
};

// MTD delta vs the same period last month (SPLM). Sign semantics: for expenses
// both `current` and `prior` are negative (money out), so the arithmetic ratio
// `(current - prior) / prior` naturally tracks magnitude direction in either
// polarity. Two cases have no meaningful percentage — a zero prior (brand-new
// account, no baseline) and a sign flip (income turned net-negative, expenses
// turned net-positive via refunds) — and render as a neutral, non-directional
// chip rather than collapsing, so every KPI card keeps a filled, aligned
// secondary row. `self-start` keeps the pill hugging its content instead of
// stretching to the card's full width.
const PILL =
    'self-start inline-flex items-center gap-[3px] px-2 py-[2px] rounded-full text-xs font-medium leading-none whitespace-nowrap';

export function MtdDeltaChip({ current, prior, polarity }: Props) {
    const noBaseline = prior.amount === 0;
    const signFlip = current.amount !== 0 && Math.sign(current.amount) !== Math.sign(prior.amount);

    if (noBaseline || signFlip) {
        return (
            <span className={cx(PILL, 'text-fg-3 bg-surface-2')}>
                {noBaseline ? <Trans>New this month</Trans> : <Trans>vs last month</Trans>}
            </span>
        );
    }

    const percent = Math.round(((current.amount - prior.amount) / prior.amount) * 100);
    const direction = percent === 0 ? 'flat' : percent > 0 ? 'up' : 'down';

    const isGood =
        (polarity === 'higher-is-good' && direction === 'up') ||
        (polarity === 'lower-is-good' && direction === 'down');

    // Typographic minus (U+2212) for the flat case so the glyph baseline aligns
    // with the ▲/▼ triangles instead of sitting on top.
    const arrow = direction === 'up' ? '▲' : direction === 'down' ? '▼' : '−';
    const colorClass =
        direction === 'flat'
            ? 'text-fg-3 bg-surface-2'
            : isGood
              ? 'text-success bg-success-soft'
              : 'text-danger bg-danger-soft';

    return (
        <span className={cx(PILL, colorClass)}>
            <span className="text-[9px]">{arrow}</span>
            <span>
                <Trans>{Math.abs(percent)}% vs Last month</Trans>
            </span>
        </span>
    );
}
