/*
 * Y-axis scaling for the money charts. Recharts' 'auto' domain hugs the data,
 * so lines and areas kiss the top and bottom of the plot and the baseline is
 * ambiguous. `moneyAxis` instead returns a domain padded by a small margin
 * (breathing room before the first and after the last value), a matching set of
 * nicely-rounded ticks, and a `truncated` flag telling the caller whether the
 * lower bound ended up above zero — the cue to render an <AxisBreakMark/>.
 *
 * Pass `includeZero` for charts that grow from a zero baseline (stacked areas,
 * bars): the axis then always contains zero and never reports as truncated,
 * which is the honest treatment for those chart types. Values are minor units
 * (ADR-0002); the returned bounds and ticks are in the same units.
 */

export type MoneyAxis = {
    domain: [number, number];
    ticks: number[];
    /** The lower bound sits above zero — the axis doesn't start at zero. */
    truncated: boolean;
};

/** Fraction of the data span left as empty margin beyond the extreme values. */
const MARGIN_FRACTION = 0.08;
/** Rough number of gridline gaps to aim for when picking a tick step. */
const TARGET_TICKS = 5;

/** Round a rough step up to the nearest 1/2/5 × 10ⁿ so tick labels stay clean. */
function niceStep(rough: number): number {
    if (rough <= 0) return 1;
    const magnitude = 10 ** Math.floor(Math.log10(rough));
    const normalized = rough / magnitude;
    const step = normalized < 1.5 ? 1 : normalized < 3 ? 2 : normalized < 7 ? 5 : 10;
    return step * magnitude;
}

export function moneyAxis(
    values: readonly number[],
    opts: { includeZero?: boolean } = {},
): MoneyAxis | undefined {
    const finite = values.filter(v => Number.isFinite(v));
    if (finite.length === 0) return undefined;

    const dataMin = Math.min(...finite);
    const dataMax = Math.max(...finite);

    let lo = dataMin;
    let hi = dataMax;
    if (opts.includeZero) {
        lo = Math.min(lo, 0);
        hi = Math.max(hi, 0);
    }

    // Pad outward by a fraction of the span so the extremes don't touch the edges.
    const span = hi - lo || Math.abs(hi) || 1;
    const margin = span * MARGIN_FRACTION;
    let paddedLo = Math.floor(lo - margin);
    let paddedHi = Math.ceil(hi + margin);
    // Don't pad across a zero baseline the caller pinned us to: an all-positive
    // (or all-negative) zero-based chart floors (or ceils) exactly at zero.
    if (opts.includeZero && dataMin >= 0) paddedLo = 0;
    if (opts.includeZero && dataMax <= 0) paddedHi = 0;

    const step = niceStep((paddedHi - paddedLo) / TARGET_TICKS);
    const ticks: number[] = [];
    for (let tick = Math.ceil(paddedLo / step) * step; tick <= paddedHi; tick += step) {
        ticks.push(tick);
    }

    return { domain: [paddedLo, paddedHi], ticks, truncated: paddedLo > 0 };
}
