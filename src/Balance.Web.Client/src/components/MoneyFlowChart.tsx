import { useMemo, useState } from 'react';
import { Rectangle, ResponsiveContainer, Sankey, Tooltip } from 'recharts';
import { useCurrencyCatalog } from '../api/currencies';
import { useMoneyFlow, type FlowDepth, type MoneyFlowNodeKind } from '../api/reports';
import { cx } from '../lib/cx';
import { formatMoney } from '../lib/money';
import type { ReportPeriod } from '../lib/reportPeriod';
import { ErrorState } from './ErrorState';
import { Panel, SectionHead } from './Panel';
import { Skeleton } from './Skeleton';

type MoneyFlowChartProps = {
    period: ReportPeriod;
    currency: string;
};

// Preset category depths offered in the header. 'all' draws the full hierarchy.
const DEPTHS: { token: FlowDepth; label: string }[] = [
    { token: 1, label: '1' },
    { token: 2, label: '2' },
    { token: 3, label: '3' },
    { token: 'all', label: 'All' },
];

// One colour per node kind. The hub is neutral; income/expense and the three
// balance-sheet types reuse the category palette so the diagram reads the same
// way as the rest of the app.
const KIND_COLOR: Record<MoneyFlowNodeKind, string> = {
    Hub: 'var(--color-fg-2)',
    Income: 'var(--color-cat-entertain)',
    Expense: 'var(--color-cat-food)',
    Asset: 'var(--color-cat-transport)',
    Liability: 'var(--color-cat-shopping)',
    Equity: 'var(--color-cat-housing)',
};

type SankeyData = {
    nodes: { name: string; kind: MoneyFlowNodeKind }[];
    links: { source: number; target: number; value: number }[];
};

export function MoneyFlowChart({ period, currency }: MoneyFlowChartProps) {
    const [depth, setDepth] = useState<FlowDepth>(1);
    const flow = useMoneyFlow(period, currency, depth);

    const data = useMemo<SankeyData | null>(() => {
        if (!flow.data) return null;
        const indexById = new Map(flow.data.nodes.map((n, i) => [n.id, i]));
        return {
            nodes: flow.data.nodes.map(n => ({ name: n.name, kind: n.kind })),
            links: flow.data.links.flatMap(l => {
                const source = indexById.get(l.source);
                const target = indexById.get(l.target);
                if (source === undefined || target === undefined) return [];
                return [{ source, target, value: l.value.amount }];
            }),
        };
    }, [flow.data]);

    return (
        <Panel>
            <SectionHead
                title="Money flow"
                subtitle="Where money came in and where it went"
                action={<DepthToggle depth={depth} onChange={setDepth} />}
            />
            {flow.isPending ? (
                <Skeleton className="h-[420px] w-full" />
            ) : flow.isError ? (
                <ErrorState
                    message="Couldn't load the money flow."
                    onRetry={() => void flow.refetch()}
                />
            ) : !data || data.links.length === 0 ? (
                <div className="h-[420px] flex items-center justify-center text-13 text-fg-3">
                    No money moved in this period.
                </div>
            ) : (
                <SankeyDiagram data={data} currency={currency} />
            )}
        </Panel>
    );
}

function DepthToggle({
    depth,
    onChange,
}: {
    depth: FlowDepth;
    onChange: (next: FlowDepth) => void;
}) {
    return (
        <div className="flex items-center gap-[6px]">
            <span className="text-11 text-fg-3">Depth</span>
            {DEPTHS.map(d => (
                <button
                    key={String(d.token)}
                    type="button"
                    onClick={() => {
                        onChange(d.token);
                    }}
                    className={cx(
                        'px-[10px] py-[5px] rounded-full text-11 font-medium select-none',
                        d.token === depth
                            ? 'bg-brand-primary-soft text-brand-primary'
                            : 'text-fg-3 hover:text-fg-1',
                    )}
                >
                    {d.label}
                </button>
            ))}
        </div>
    );
}

function SankeyDiagram({ data, currency }: { data: SankeyData; currency: string }) {
    const catalog = useCurrencyCatalog();
    // Give every node room to breathe; tall charts stay legible.
    const height = Math.max(360, data.nodes.length * 30);

    return (
        <ResponsiveContainer width="100%" height={height}>
            <Sankey
                data={data}
                nodePadding={26}
                nodeWidth={12}
                linkCurvature={0.5}
                iterations={64}
                align="left"
                margin={{ top: 20, right: 120, bottom: 20, left: 120 }}
                link={{ stroke: '#2b2b2b', strokeOpacity: 1 }}
                node={<FlowNode />}
            >
                <Tooltip
                    formatter={value => formatMoney(Number(value), currency, catalog)}
                    contentStyle={{
                        background: 'var(--color-bg-1)',
                        border: '1px solid var(--color-border-soft)',
                        borderRadius: 6,
                        fontSize: 12,
                        color: 'var(--color-fg-1)',
                    }}
                    itemStyle={{ color: 'var(--color-fg-1)' }}
                />
            </Sankey>
        </ResponsiveContainer>
    );
}

type FlowNodeProps = {
    x?: number;
    y?: number;
    width?: number;
    height?: number;
    index?: number;
    // recharts augments each node with its graph links; `sourceLinks` are the
    // edges flowing *into* this node, so an empty list means it's a pure source.
    payload?: { name: string; kind: MoneyFlowNodeKind; sourceLinks?: unknown[] };
};

// Custom Sankey node: a coloured bar with its account name placed on the
// outward side — pure sources (income, drawdowns) on the left, everything from
// the hub rightward (expenses, savings) on the right — so labels never sit on
// top of the flows.
function FlowNode({ x, y, width, height, payload }: FlowNodeProps) {
    if (
        x === undefined ||
        y === undefined ||
        width === undefined ||
        height === undefined ||
        !payload
    ) {
        return null;
    }

    const color = KIND_COLOR[payload.kind];
    const isSource = (payload.sourceLinks?.length ?? 0) === 0;
    const labelX = isSource ? x - 8 : x + width + 8;
    const anchor = isSource ? 'end' : 'start';

    return (
        <g>
            <Rectangle x={x} y={y} width={width} height={height} fill={color} fillOpacity={0.9} />
            <text
                x={labelX}
                y={y + height / 2}
                textAnchor={anchor}
                dominantBaseline="middle"
                fontSize={12}
                fill="var(--color-fg-2)"
            >
                {payload.name}
            </text>
        </g>
    );
}
