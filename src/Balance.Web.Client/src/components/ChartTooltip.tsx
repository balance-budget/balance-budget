import type { ReactNode } from 'react';

// The single source of truth for chart tooltip chrome. Every chart's tooltip
// (line, area, bar, pie, sankey) renders through these primitives so they all
// read identically: same card, same colored-dot legend rows, same right-aligned
// monospace values, and the same separated "total" row for stacked charts.

/** The tooltip card: a heading (usually the hovered date) above a column of rows. */
export function ChartTooltipShell({
    heading,
    children,
}: {
    heading?: ReactNode;
    children: ReactNode;
}) {
    return (
        <div className="rounded-xl border border-border-soft bg-bg-1 px-3 py-2 shadow-sm text-xs">
            {heading !== undefined && heading !== '' && (
                <div className="text-fg-3 mb-1">{heading}</div>
            )}
            <div className="flex flex-col gap-1">{children}</div>
        </div>
    );
}

/**
 * One series row: a colored dot, the series name, and its value. `color`
 * defaults to white for rows that aren't a colored series (e.g. a derived
 * figure or a chart whose payload carries no per-series hue).
 */
export function ChartTooltipRow({
    color = 'var(--color-fg-1)',
    name,
    value,
}: {
    color?: string;
    name: ReactNode;
    value: ReactNode;
}) {
    return (
        <div className="flex items-center justify-between gap-x-4">
            <span className="flex items-center gap-1.5">
                <span
                    className="w-2 h-2 rounded-full inline-block"
                    style={{ background: color }}
                />
                <span className="text-fg-2">{name}</span>
            </span>
            <span className="font-mono tabular-nums text-fg-1">{value}</span>
        </div>
    );
}

/** The stacked-chart total: same layout as a row but separated by a top border. */
export function ChartTooltipTotalRow({ name, value }: { name: ReactNode; value: ReactNode }) {
    return (
        <div className="flex items-center justify-between gap-x-4 mt-1 pt-1 border-t border-border-soft">
            <span className="flex items-center gap-1.5">
                <span className="w-2 h-2" />
                <span className="text-fg-2">{name}</span>
            </span>
            <span className="font-mono tabular-nums text-fg-1">{value}</span>
        </div>
    );
}
