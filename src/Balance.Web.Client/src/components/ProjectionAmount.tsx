import { useCurrencyCatalog } from '../api/currencies';
import { cx } from '../lib/cx';
import type { JournalProjection } from '../lib/journalProjection';
import { formatMoney, type Money } from '../lib/money';
import { Amount } from './Amount';

/**
 * The ADR-0011 amount shown on a one-row entry summary: transfers
 * (`netWorthChange === 0`) render the unsigned gross magnitude in muted text;
 * operating entries render the signed net-worth change colored by sign. Single
 * source for that rule across the Activity feed, the Counterparty register, and
 * the Journal detail header. `variant` selects the surface — the mono list-row
 * cell or the big detail-header figure.
 */
export function ProjectionAmount({
    projection,
    variant,
}: {
    projection: JournalProjection;
    variant: 'row' | 'header';
}) {
    const money = projection.isTransfer ? projection.grossMagnitude : projection.netWorthChange;
    const color = projection.isTransfer
        ? 'text-fg-3'
        : money.amount < 0
          ? 'text-danger'
          : 'text-success';
    const sign = !projection.isTransfer;

    if (variant === 'header') {
        return (
            <Amount
                minor={money.amount}
                currencyCode={money.currencyCode}
                size="big"
                sign={sign}
                className={color}
            />
        );
    }
    return <RowAmount money={money} color={color} sign={sign} />;
}

function RowAmount({ money, color, sign }: { money: Money; color: string; sign: boolean }) {
    const catalog = useCurrencyCatalog();
    return (
        <span className={cx('font-mono text-sm tabular-nums text-right', color)}>
            {formatMoney(money.amount, money.currencyCode, catalog, { sign })}
        </span>
    );
}
