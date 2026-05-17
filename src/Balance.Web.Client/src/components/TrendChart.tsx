import type { AccountTrend } from '../lib/domain';

type TrendChartProps = {
    series: AccountTrend[];
    days: number;
    today: number;
    height?: number;
};

/**
 * Multi-account balance trend. Each series is plotted as a polyline,
 * dashed past the "today" marker (forecast). A vertical hairline marks
 * today, and a faint zero-line sits across the middle for sign reference.
 */
export function TrendChart({ series, days, today, height = 240 }: TrendChartProps) {
    const width = 720;
    const padX = 4;
    const padY = 20;
    const plotW = width - padX * 2;
    const plotH = height - padY * 2;

    const allPoints = series.flatMap(s => s.points.map(p => p.balanceMinor));
    const min = Math.min(...allPoints, 0);
    const max = Math.max(...allPoints, 0);
    const range = max - min || 1;

    const xOf = (day: number) => padX + (day / (days - 1)) * plotW;
    const yOf = (val: number) => padY + (1 - (val - min) / range) * plotH;

    const zeroY = yOf(0);
    const todayX = xOf(today);

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

            {/* Today marker */}
            <line
                x1={todayX}
                x2={todayX}
                y1={padY - 4}
                y2={height - padY + 4}
                stroke="var(--color-border-strong)"
                strokeDasharray="2 4"
            />
            <text
                x={todayX + 6}
                y={padY + 2}
                fontSize="10"
                fill="var(--color-fg-3)"
                fontFamily="var(--font-mono)"
            >
                Today
            </text>

            {/* One polyline per series, split into solid (past) + dashed (future) halves */}
            {series.map(s => {
                const pastPts = s.points.slice(0, today + 1);
                const futurePts = s.points.slice(today);
                const toD = (pts: typeof s.points) =>
                    pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xOf(p.day)} ${yOf(p.balanceMinor)}`).join(' ');
                return (
                    <g key={s.accountId}>
                        <path
                            d={toD(pastPts)}
                            fill="none"
                            stroke={s.accentColor}
                            strokeWidth={1.75}
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        />
                        <path
                            d={toD(futurePts)}
                            fill="none"
                            stroke={s.accentColor}
                            strokeWidth={1.75}
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeDasharray="3 4"
                            opacity={0.55}
                        />
                    </g>
                );
            })}
        </svg>
    );
}
