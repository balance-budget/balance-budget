import { usePlotArea } from 'recharts';

/** Pixels the zigzag is lifted off the x-axis so it sits at the foot of the y
 *  axis without crowding the first x-axis tick label, which shares the same
 *  left-edge column as the y-axis. */
const LIFT = 3;

/**
 * A broken-axis squiggle drawn near the base of the Y axis. Recharts has no
 * native truncated-axis indicator, so this small zigzag is the cue that the y
 * scale doesn't start at zero. Render it as a direct child of the chart
 * (recharts 3.8+ resolves layout hooks for arbitrary chart children) and only
 * when the axis is truncated — see `moneyAxis().truncated` in lib/chartAxis.
 */
export function AxisBreakMark() {
    const plot = usePlotArea();
    if (!plot) return null;

    // The Y axis sits at the left edge of the plot; the zigzag straddles it just
    // above the x-axis, returning to center so it reads as a clean break symbol.
    const x = plot.x;
    const base = plot.y + plot.height - LIFT;
    // eslint-disable-next-line lingui/no-unlocalized-strings -- SVG path geometry, not UI copy.
    const d = `M ${x} ${base} l -4 -3 l 8 -3 l -8 -3 l 4 -3`;

    return (
        <path
            d={d}
            fill="none"
            stroke="var(--color-fg-3)"
            strokeWidth={1.5}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        />
    );
}
