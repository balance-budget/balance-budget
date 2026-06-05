import { useCurrencyCatalog } from '../api/currencies';
import { cx } from '../lib/cx';
import type { JournalProjection } from '../lib/journalProjection';
import { formatMoney, type Money } from '../lib/money';
import { Amount } from './Amount';

/**
 * The ADR-0011 amount shown on a one-row entry summary: transfers
 * (`netWorthChange === 0`) render the unsigned gross magnitude in muted text;
 * operating entries render the signed net-worth change coloured by sign. Single
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
    const colour = projection.isTransfer
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
                className={colour}
            />
        );
    }
    return <RowAmount money={money} colour={colour} sign={sign} />;
}

function RowAmount({ money, colour, sign }: { money: Money; colour: string; sign: boolean }) {
    const catalog = useCurrencyCatalog();
    return (
        <span className={cx('font-mono text-13 tabular text-right', colour)}>
            {formatMoney(money.amount, money.currencyCode, catalog, { sign })}
        </span>
    );
}
