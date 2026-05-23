/*
 * Money helpers — see ADR-0002. Amounts are integer minor units; conversion
 * to display strings is a pure presentation concern that happens here.
 */

import type { components } from './api-types';

type WireMoney = components['schemas']['Money'];
export type Money = { amount: number; currencyCode: string };

/**
 * Boundary converter for wire-format Money. openapi-typescript marks both fields
 * optional (System.Text.Json on a record struct), and large ints may serialise
 * as strings — both are normalised here. The fallback currency code is the
 * envelope's currency (account, summary, etc.) when the inner value lacks one.
 */
export function toMoney(wire: WireMoney, fallbackCurrencyCode: string): Money {
    const raw = wire.amount;
    const amount = typeof raw === 'string' ? Number(raw) : (raw ?? 0);
    return {
        amount,
        currencyCode: wire.currencyCode ?? fallbackCurrencyCode,
    };
}

// Mirrors the backend's seeded currencies (Balance.Data.Configurations.CurrencySeed).
// User-defined currencies fall back to the defaults below — refetch the currency
// catalog from the API once non-seeded currencies become a real scenario.
const MINOR_UNIT_SCALE: Record<string, number> = {
    EUR: 2,
    USD: 2,
    GBP: 2,
    CHF: 2,
    JPY: 0,
    BTC: 8,
    ETH: 18,
};

const CURRENCY_SYMBOL: Record<string, string> = {
    EUR: '€',
    USD: '$',
    GBP: '£',
    CHF: 'CHF ',
    JPY: '¥',
};

const DEFAULT_SCALE = 2;

function minorUnitScale(currencyCode: string): number {
    return MINOR_UNIT_SCALE[currencyCode] ?? DEFAULT_SCALE;
}

function currencySymbol(currencyCode: string): string {
    return CURRENCY_SYMBOL[currencyCode] ?? currencyCode + ' ';
}

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
    const scale = minorUnitScale(currencyCode);
    const symbol = currencySymbol(currencyCode);

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
export function formatMoney(minor: number, currencyCode: string, opts: FormatOptions = {}): string {
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
    const scale = minorUnitScale(currencyCode);
    const symbol = currencySymbol(currencyCode);
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
