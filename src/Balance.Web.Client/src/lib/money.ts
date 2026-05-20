/*
 * Money helpers — see ADR-0002. Amounts are integer minor units; conversion
 * to display strings is a pure presentation concern that happens here.
 */

const MINOR_UNIT_SCALE: Record<string, number> = {
    EUR: 2,
    USD: 2,
    GBP: 2,
    JPY: 0,
    BTC: 8,
};

const CURRENCY_SYMBOL: Record<string, string> = {
    EUR: '€',
    USD: '$',
    GBP: '£',
    JPY: '¥',
};

export type FormatOptions = {
    /** Render the integer portion only — drops the decimal tail (e.g. tight summary numbers). */
    decimals?: boolean;
    /** Force a leading + on positive amounts. Negative always uses U+2212 (typographic minus). */
    sign?: boolean;
};

export type FormattedMoney = {
    symbol: string;
    integer: string;
    fraction: string;
    sign: '+' | '−' | '';
};

const PLUS = '+';
const MINUS = '−';

/**
 * Split a minor-units amount into the parts a UI needs to render: currency
 * symbol, integer digits, fractional digits, and a leading sign. The caller
 * decides how to colour each piece (the design dims the symbol and the
 * fractional tail).
 */
export function splitMoney(
    minor: number,
    currencyCode: string,
    opts: FormatOptions = {},
): FormattedMoney {
    const scale = MINOR_UNIT_SCALE[currencyCode] ?? 2;
    const symbol = CURRENCY_SYMBOL[currencyCode] ?? currencyCode + ' ';

    const negative = minor < 0;
    const absMinor = Math.abs(minor);

    const divisor = 10 ** scale;
    const integerPart = Math.floor(absMinor / divisor);
    const fractionPart = absMinor - integerPart * divisor;

    const integerStr = integerPart.toLocaleString('en-US');
    const fractionStr = scale > 0 ? fractionPart.toString().padStart(scale, '0') : '';

    let sign: '+' | '−' | '' = '';
    if (negative) sign = MINUS;
    else if (opts.sign && minor > 0) sign = PLUS;

    return {
        symbol,
        integer: integerStr,
        fraction: opts.decimals === false ? '' : fractionStr,
        sign,
    };
}

/** Render the parts as a single string, when you don't need styled spans. */
export function formatMoney(
    minor: number,
    currencyCode: string,
    opts: FormatOptions = {},
): string {
    const m = splitMoney(minor, currencyCode, opts);
    const sep = m.fraction ? '.' : '';
    return `${m.sign}${m.symbol}${m.integer}${sep}${m.fraction}`;
}

/**
 * Compact chart-axis label. Whole major units below 10,000 (e.g. €1,234), and
 * k/M abbreviations above (€12k, €1.2M). Tooltips use full formatMoney; this
 * is for the y-axis ticks where precision yields to legibility.
 */
export function formatMoneyAxis(minor: number, currencyCode: string): string {
    const scale = MINOR_UNIT_SCALE[currencyCode] ?? 2;
    const symbol = CURRENCY_SYMBOL[currencyCode] ?? currencyCode + ' ';
    const negative = minor < 0;
    const absMajor = Math.abs(minor) / 10 ** scale;
    const sign = negative ? MINUS : '';

    if (absMajor < 10_000) {
        const integer = Math.round(absMajor).toLocaleString('en-US');
        return `${sign}${symbol}${integer}`;
    }

    if (absMajor < 1_000_000) {
        const k = absMajor / 1_000;
        const rounded = k >= 100 ? Math.round(k) : Math.round(k * 10) / 10;
        return `${sign}${symbol}${rounded}k`;
    }

    const m = absMajor / 1_000_000;
    const rounded = m >= 100 ? Math.round(m) : Math.round(m * 10) / 10;
    return `${sign}${symbol}${rounded}M`;
}
