import { cx } from '../lib/cx';
import { splitMoney, type FormatOptions } from '../lib/money';

type AmountProps = {
    minor: number;
    currencyCode: string;
    /** big = 44px / medium = 22px / inline = 14px tabular */
    size?: 'big' | 'medium' | 'inline';
    className?: string;
} & FormatOptions;

const SIZE_CLASS: Record<NonNullable<AmountProps['size']>, string> = {
    big: 'text-[44px] font-medium leading-none tracking-[-0.01em]',
    medium: 'text-[22px] font-medium leading-tight',
    inline: 'text-14 font-medium leading-none',
};

const CENTS_SCALE: Record<NonNullable<AmountProps['size']>, string> = {
    big: 'text-[0.62em]',
    medium: 'text-[0.62em]',
    inline: 'text-[0.85em]',
};

/**
 * Render a Money value with the design system's three-tier emphasis: dim
 * currency symbol, full-weight integer, dim fractional tail. Always
 * tabular-aligned.
 */
export function Amount({ minor, currencyCode, size = 'medium', className, ...fmt }: AmountProps) {
    const m = splitMoney(minor, currencyCode, fmt);
    return (
        <span className={cx('tabular inline-flex items-baseline', SIZE_CLASS[size], className)}>
            {m.sign && <span className="mr-[0.05em]">{m.sign}</span>}
            <span className="text-fg-3 font-normal mr-[0.1em]">{m.symbol}</span>
            <span>{m.integer}</span>
            {m.fraction && (
                <span className={cx('text-fg-2 ml-[1px]', CENTS_SCALE[size])}>.{m.fraction}</span>
            )}
        </span>
    );
}
