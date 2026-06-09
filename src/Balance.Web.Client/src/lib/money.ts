/*
 * Money helpers — see ADR-0002. Amounts are integer minor units; conversion
 * to display strings is a pure presentation concern that happens here.
 *
 * Currency metadata (minor-unit scale, symbol) comes from the backend's
 * /api/currencies catalog via `useCurrencyCatalog()` and is passed in to
 * splitMoney / formatMoney / formatMoneyAxis. The catalog is a ReadonlyMap;
 * a missing entry falls back to two decimals and the bare code as the symbol
 * (e.g. "XYZ 1.00") so first-paint before the catalog resolves is graceful.
 */

import type { Currency, CurrencyCatalog } from '../api/currencies';
import { activeDecimalSeparator, activeNumberLocale } from '../i18n/format';
import type { components } from './api-types.gen';

type WireMoney = components['schemas']['Money'];
export type Money = { amount: number; currencyCode: string };

/**
 * Boundary converter for a single wire number: System.Text.Json serialises large
 * 64-bit integers as strings, so wire numerics arrive as `number | string`. This
 * normalises them back to `number`.
 */
export function toNumber(raw: number | string): number {
    return typeof raw === 'string' ? Number(raw) : raw;
}

/**
 * Boundary converter for wire-format Money. openapi-typescript marks both fields
 * optional (System.Text.Json on a record struct), but the backend contract
 * guarantees both are present — large ints may serialise as strings, which is
 * normalised to number here. `fallbackCurrencyCode` lets envelope-shaped
 * payloads (DashboardSummaryOutput, AccountOutput, ...) supply their outer
 * currency as a belt-and-suspenders default; pass it when available, omit it
 * when the wire Money is the only source of truth. Throws if currency code is
 * missing from both wire and fallback — that indicates a contract violation.
 */
export function toMoney(wire: WireMoney, fallbackCurrencyCode?: string): Money {
    const raw = wire.amount;
    const amount = typeof raw === 'string' ? Number(raw) : (raw ?? 0);
    const currencyCode = wire.currencyCode ?? fallbackCurrencyCode;
    if (currencyCode === undefined) {
        throw new Error('Money wire payload is missing currencyCode and no fallback was provided');
    }
    return { amount, currencyCode };
}

const DEFAULT_SCALE = 2;

type DisplaySpec = { scale: number; symbol: string };

function displaySpec(currencyCode: string, catalog: CurrencyCatalog): DisplaySpec {
    const currency = catalog.get(currencyCode);
    if (!currency) {
        return { scale: DEFAULT_SCALE, symbol: trailingSpaceIfMultichar(currencyCode) };
    }
    return {
        scale: currency.minorUnitScale,
        symbol: symbolFor(currency, currencyCode),
    };
}

function symbolFor(currency: Currency, code: string): string {
    const raw = currency.symbol ?? code;
    return trailingSpaceIfMultichar(raw);
}

// Multi-character "symbols" (CHF, NOK, BTC fallback) read better with a
// trailing space; single glyphs like € $ £ ¥ ₿ Ξ do not.
function trailingSpaceIfMultichar(symbol: string): string {
    return symbol.length > 1 ? `${symbol} ` : symbol;
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
    catalog: CurrencyCatalog,
    opts: FormatOptions = {},
): FormattedMoney {
    const { scale, symbol } = displaySpec(currencyCode, catalog);

    const negative = minor < 0;
    const absMinor = Math.abs(minor);

    const divisor = 10 ** scale;
    const integerPart = Math.floor(absMinor / divisor);
    const fractionPart = absMinor - integerPart * divisor;

    const integerStr = integerPart.toLocaleString(activeNumberLocale());
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
    catalog: CurrencyCatalog,
    opts: FormatOptions = {},
): string {
    const m = splitMoney(minor, currencyCode, catalog, opts);
    const sep = m.fraction ? activeDecimalSeparator() : '';
    return `${m.sign}${m.symbol}${m.integer}${sep}${m.fraction}`;
}

export type ParseMoneyResult = { ok: true; minor: number } | { ok: false; error: string };

/**
 * User-input boundary parser. Takes a major-units string ("12.34", "1,234.50",
 * "-7") and converts to a minor-units integer against the supplied scale.
 * Rejects strings with more decimal places than the currency allows rather
 * than silently rounding — losing precision on the input boundary would
 * violate ADR-0002 (minor-unit precision is exact at the boundaries).
 *
 * Accepts an optional leading sign, optional thousands separators (comma or
 * space), and a `.` decimal mark. Trims whitespace. Empty / whitespace-only
 * is reported as "Required" so callers can render it as a field error.
 */
export function parseMoney(input: string, scale: number): ParseMoneyResult {
    const trimmed = input.trim();
    if (trimmed.length === 0) {
        return { ok: false, error: 'Required' };
    }

    const match = /^([+\-−])?([\d,\s]+)(?:\.(\d+))?$/.exec(trimmed);
    if (!match) {
        return { ok: false, error: 'Enter a number, e.g. 12.34' };
    }

    const [, signGroup, integerGroup, fractionGroup] = match;
    const sign = signGroup === '-' || signGroup === '−' ? -1 : 1;
    const integerDigits = (integerGroup ?? '').replace(/[,\s]/g, '');
    const fractionDigits = fractionGroup ?? '';

    if (integerDigits.length === 0) {
        return { ok: false, error: 'Enter a number, e.g. 12.34' };
    }

    if (fractionDigits.length > scale) {
        return {
            ok: false,
            error: scale === 0 ? 'Whole numbers only' : `At most ${scale} decimal places`,
        };
    }

    const paddedFraction = fractionDigits.padEnd(scale, '0');
    const combined = `${integerDigits}${paddedFraction}`;
    const magnitude = Number(combined);

    if (!Number.isFinite(magnitude) || !Number.isSafeInteger(magnitude)) {
        return { ok: false, error: 'Amount is too large' };
    }

    return { ok: true, minor: sign * magnitude };
}

/**
 * Compact chart-axis label. Whole major units below 10,000 (e.g. €1,234), and
 * k/M abbreviations above (€12k, €1.2M). Tooltips use full formatMoney; this
 * is for the y-axis ticks where precision yields to legibility.
 */
export function formatMoneyAxis(
    minor: number,
    currencyCode: string,
    catalog: CurrencyCatalog,
): string {
    const { scale, symbol } = displaySpec(currencyCode, catalog);
    const negative = minor < 0;
    const absMajor = Math.abs(minor) / 10 ** scale;
    const sign = negative ? MINUS : '';

    if (absMajor < 10_000) {
        const integer = Math.round(absMajor).toLocaleString(activeNumberLocale());
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
