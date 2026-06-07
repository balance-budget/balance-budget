import { useCallback, useMemo, useState } from 'react';
import { Rectangle, ResponsiveContainer, Sankey, Tooltip } from 'recharts';
import { useCurrencyCatalog } from '../api/currencies';
import { useMoneyFlow, type MoneyFlowNode, type MoneyFlowNodeKind } from '../api/reports';
import { formatMoney } from '../lib/money';
import type { ReportPeriod } from '../lib/reportPeriod';
import { ErrorState } from './ErrorState';
import { Panel, SectionHead } from './Panel';
import { Skeleton } from './Skeleton';

type MoneyFlowChartProps = {
    period: ReportPeriod;
    currency: string;
};

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

type SankeyNode = { id: string; name: string; kind: MoneyFlowNodeKind; hasChildren: boolean };
type SankeyData = {
    nodes: SankeyNode[];
    links: { source: number; target: number; value: number }[];
};

// Collapsing a node hides its whole subtree, so any of its descendants that were
// themselves expanded must drop out of the set — otherwise re-expanding the node
// would resurface a deep tree (we chose prune-on-collapse). The descendants are
// still visible at collapse time, so we walk parentId among the current nodes.
function collapse(expanded: Set<string>, id: string, nodes: readonly MoneyFlowNode[]): Set<string> {
    const childrenByParent = new Map<string, string[]>();
    for (const n of nodes) {
        if (n.parentId === null) continue;
        const siblings = childrenByParent.get(n.parentId) ?? [];
        siblings.push(n.id);
        childrenByParent.set(n.parentId, siblings);
    }

    const next = new Set(expanded);
    const stack = [id];
    for (let current = stack.pop(); current !== undefined; current = stack.pop()) {
        next.delete(current);
        for (const child of childrenByParent.get(current) ?? []) stack.push(child);
    }
    return next;
}

export function MoneyFlowChart({ period, currency }: MoneyFlowChartProps) {
    const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
    const expandedIds = useMemo(() => [...expanded], [expanded]);
    const flow = useMoneyFlow(period, currency, expandedIds);

    const toggle = useCallback(
        (id: string) => {
            setExpanded(prev => {
                if (prev.has(id)) return collapse(prev, id, flow.data?.nodes ?? []);
                const next = new Set(prev);
                next.add(id);
                return next;
            });
        },
        [flow.data],
    );

    const data = useMemo<SankeyData | null>(() => {
        if (!flow.data) return null;
        const indexById = new Map(flow.data.nodes.map((n, i) => [n.id, i]));
        return {
            nodes: flow.data.nodes.map(n => ({
                id: n.id,
                name: n.name,
                kind: n.kind,
                hasChildren: n.hasChildren,
            })),
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
                action={
                    expanded.size > 0 ? (
                        <button
                            type="button"
                            onClick={() => {
                                setExpanded(new Set());
                            }}
                            className="px-[10px] py-[5px] rounded-full text-xs font-medium text-fg-3 hover:text-fg-1 select-none"
                        >
                            Collapse all
                        </button>
                    ) : undefined
                }
            />
            {flow.isPending ? (
                <Skeleton className="h-[420px] w-full" />
            ) : flow.isError ? (
                <ErrorState
                    message="Couldn't load the money flow."
                    onRetry={() => void flow.refetch()}
                />
            ) : !data || data.links.length === 0 ? (
                <div className="h-[420px] flex items-center justify-center text-sm text-fg-3">
                    No money moved in this period.
                </div>
            ) : (
                <SankeyDiagram
                    data={data}
                    currency={currency}
                    expanded={expanded}
                    onToggle={toggle}
                />
            )}
        </Panel>
    );
}

function SankeyDiagram({
    data,
    currency,
    expanded,
    onToggle,
}: {
    data: SankeyData;
    currency: string;
    expanded: Set<string>;
    onToggle: (id: string) => void;
}) {
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
                node={<FlowNode expanded={expanded} onToggle={onToggle} />}
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
    // recharts augments each node with its graph links; `sourceLinks` are the
    // edges flowing *into* this node, so an empty list means it's a pure source.
    payload?: {
        id: string;
        name: string;
        kind: MoneyFlowNodeKind;
        hasChildren: boolean;
        sourceLinks?: unknown[];
    };
    // Passed through by SankeyDiagram (recharts clones the element with our props).
    expanded?: Set<string>;
    onToggle?: (id: string) => void;
};

// Custom Sankey node: a coloured bar with its account name placed on the
// outward side — pure sources (income, drawdowns) on the left, everything from
// the hub rightward (expenses, savings) on the right — so labels never sit on
// top of the flows. Nodes with children carry a circled +/− on the outward edge
// of the label (⊕ collapsed, ⊖ expanded) and toggle their subtree on click.
function FlowNode({ x, y, width, height, payload, expanded, onToggle }: FlowNodeProps) {
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

    const expandable = payload.hasChildren;
    const isExpanded = expandable && (expanded?.has(payload.id) ?? false);
    // ⊕ to expand, ⊖ to collapse; mirrored to the outward edge of the label so it
    // sits at the far end on both wings (left of income labels, right of expense).
    const glyph = !expandable ? '' : isExpanded ? '⊖' : '⊕';
    const label = !glyph
        ? payload.name
        : isSource
          ? `${glyph}  ${payload.name}`
          : `${payload.name}  ${glyph}`;

    return (
        <g
            onClick={expandable ? () => onToggle?.(payload.id) : undefined}
            style={{ cursor: expandable ? 'pointer' : 'default' }}
        >
            <Rectangle x={x} y={y} width={width} height={height} fill={color} fillOpacity={0.9} />
            <text
                x={labelX}
                y={y + height / 2}
                textAnchor={anchor}
                dominantBaseline="middle"
                fontSize={12}
                fill="var(--color-fg-2)"
            >
                {label}
            </text>
        </g>
    );
}
