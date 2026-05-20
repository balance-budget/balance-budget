import type { AccountTrend } from '../lib/domain';

type TrendChartProps = {
    series: AccountTrend[];
    days: number;
    height?: number;
};

/**
 * Multi-account balance trend. Each series is plotted as a solid polyline
 * across the full window. A faint zero-line sits across the middle for sign
 * reference. The chart ends at today — there is no forecast / future half.
 */
export function TrendChart({ series, days, height = 240 }: TrendChartProps) {
    const width = 720;
    const padX = 4;
    const padY = 20;
    const plotW = width - padX * 2;
    const plotH = height - padY * 2;

    const allPoints = series.flatMap(s => s.points.map(p => p.balanceMinor));
    const min = Math.min(...allPoints, 0);
    const max = Math.max(...allPoints, 0);
    const range = max - min || 1;
    const denom = days > 1 ? days - 1 : 1;

    const xOf = (day: number) => padX + (day / denom) * plotW;
    const yOf = (val: number) => padY + (1 - (val - min) / range) * plotH;

    const zeroY = yOf(0);

    return (
        <svg viewBox={`0 0 ${width} ${height}`} className="w-full h-auto block">
            {/* Zero line */}
            <line
                x1={padX}
                x2={width - padX}
                y1={zeroY}
                y2={zeroY}
                stroke="var(--color-border-soft)"
                strokeDasharray="2 4"
            />

            {series.map(s => {
                const d = s.points
                    .map(
                        (p, i) =>
                            `${i === 0 ? 'M' : 'L'} ${xOf(p.day)} ${yOf(p.balanceMinor)}`,
                    )
                    .join(' ');
                return (
                    <path
                        key={s.accountId}
                        d={d}
                        fill="none"
                        stroke={s.accentColor}
                        strokeWidth={1.75}
                        strokeLinecap="round"
                        strokeLinejoin="round"
                    />
                );
            })}
        </svg>
    );
}
