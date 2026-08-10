import { Trans, useLingui } from '@lingui/react/macro';
import { useCallback, useMemo, useState } from 'react';
import { defineChart, link, rect, text } from '@tanstack/charts';
import { d3Curve } from '@tanstack/charts/d3/shape';
import { sankeyDiagram, type SankeyLink, type SankeyNode } from '@tanstack/charts/network/sankey';
import { curveBumpX } from 'd3-shape';
import { useMoneyFlow, type MoneyFlowNode, type MoneyFlowNodeKind } from '../api/reports';
import { ACCENT_BY_TYPE } from '../lib/visualHints';
import type { ReportPeriod } from '../lib/reportPeriod';
import { chartTooltip } from '../lib/chart';
import { Chart } from './Chart';
import { ErrorState } from './ErrorState';
import { Panel, SectionHead } from './Panel';
import { Skeleton } from './Skeleton';

type MoneyFlowChartProps = {
    period: ReportPeriod;
    currency: string;
};

// One color per node kind. The hub is neutral; the five AccountType kinds reuse
// the single ACCENT_BY_TYPE source so the diagram reads the same way as avatars
// and the distribution donut (and the two can never drift apart).
const KIND_COLOR: Record<MoneyFlowNodeKind, string> = {
    Hub: 'var(--color-fg-2)',
    ...ACCENT_BY_TYPE,
};

type FlowNode = { id: string; name: string; kind: MoneyFlowNodeKind; hasChildren: boolean };
type FlowLink = { source: string; target: string; value: number };
type FlowData = { nodes: FlowNode[]; links: FlowLink[] };

/** Pixels between a node bar and its label. */
const LABEL_GAP = 8;
/** Room reserved on both sides of the diagram for the node labels. */
const LABEL_INSET = 120;

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
    const { t } = useLingui();
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

    const data = useMemo<FlowData | null>(() => {
        if (!flow.data) return null;
        const ids = new Set(flow.data.nodes.map(n => n.id));
        return {
            nodes: flow.data.nodes.map(n => ({
                id: n.id,
                name: n.name,
                kind: n.kind,
                hasChildren: n.hasChildren,
            })),
            links: flow.data.links.flatMap(l =>
                ids.has(l.source) && ids.has(l.target)
                    ? [{ source: l.source, target: l.target, value: l.value.amount }]
                    : [],
            ),
        };
    }, [flow.data]);

    return (
        <Panel>
            <SectionHead
                title={<Trans>Money flow</Trans>}
                subtitle={<Trans>Where money came in and where it went</Trans>}
                action={
                    expanded.size > 0 ? (
                        <button
                            type="button"
                            onClick={() => {
                                setExpanded(new Set());
                            }}
                            className="px-[10px] py-[5px] rounded-full text-xs font-medium text-fg-3 hover:text-fg-1 select-none"
                        >
                            <Trans>Collapse all</Trans>
                        </button>
                    ) : undefined
                }
            />
            {flow.isPending ? (
                <Skeleton className="h-[420px] w-full" />
            ) : flow.isError ? (
                <ErrorState
                    message={t`Couldn't load the money flow.`}
                    onRetry={() => void flow.refetch()}
                />
            ) : !data || data.links.length === 0 ? (
                <div className="h-[420px] flex items-center justify-center text-sm text-fg-3">
                    <Trans>No money moved in this period.</Trans>
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
    data: FlowData;
    currency: string;
    expanded: Set<string>;
    onToggle: (id: string) => void;
}) {
    const { t } = useLingui();
    // Give every node room to breathe; tall charts stay legible.
    const height = Math.max(360, data.nodes.length * 30);

    const definition = useMemo(
        () =>
            defineChart({
                marks: [
                    sankeyDiagram({
                        nodes: data.nodes,
                        links: data.links,
                        nodeKey: 'id',
                        source: 'source',
                        target: 'target',
                        value: 'value',
                        align: 'left',
                        nodeWidth: 12,
                        nodePadding: 26,
                        iterations: 64,
                        inset: { top: 20, right: LABEL_INSET, bottom: 20, left: LABEL_INSET },
                        marks: ({ nodes, links }) =>
                            [
                                link(links, {
                                    x1: 'x1',
                                    y1: 'y1',
                                    x2: 'x2',
                                    y2: 'y2',
                                    key: 'key',
                                    curve: d3Curve(curveBumpX),
                                    strokeWidth: flow => Math.max(1, flow.width),
                                    stroke: 'var(--color-fg-4)',
                                    strokeOpacity: 0.6,
                                    lineCap: 'butt',
                                }),
                                rect(nodes, {
                                    x1: 'x0',
                                    x2: 'x1',
                                    y1: 'y0',
                                    y2: 'y1',
                                    key: 'key',
                                    color: node => node.data.kind,
                                    fillOpacity: 0.9,
                                    inset: 0,
                                }),
                                text(nodes, {
                                    x: node => (isSource(node) ? node.x0 : node.x1),
                                    y: 'y',
                                    key: 'key',
                                    text: node => label(node, expanded),
                                    anchor: node => (isSource(node) ? 'end' : 'start'),
                                    dx: node => (isSource(node) ? -LABEL_GAP : LABEL_GAP),
                                    fontSize: 12,
                                    fill: 'var(--color-fg-2)',
                                }),
                            ] as const,
                    }),
                ],
                color: {
                    domain: Object.keys(KIND_COLOR),
                    range: Object.values(KIND_COLOR),
                },
                guides: false,
                tooltip: chartTooltip,
            }),
        [data, expanded],
    );

    return (
        <Chart
            definition={definition}
            height={height}
            currency={currency}
            ariaLabel={t`Money flow between accounts`}
            tooltipRows={(points, formatValue) =>
                points.map(point => ({
                    color: point.color,
                    name: describe(point.datum),
                    value: formatValue(point.datum.value),
                }))
            }
            onSelect={point => {
                const datum = point?.datum;
                if (datum?.kind === 'node' && datum.data.hasChildren) onToggle(datum.data.id);
            }}
        />
    );
}

type LayoutNode = SankeyNode<FlowNode, FlowLink>;

/** A pure source (income, drawdowns) has nothing flowing into it, so its label sits on the left. */
function isSource(node: LayoutNode): boolean {
    return node.incomingLinks.length === 0;
}

/**
 * The node name, prefixed or suffixed with a circled toggle on the outward edge
 * of the label (⊕ collapsed, ⊖ expanded) when the node has a subtree.
 */
function label(node: LayoutNode, expanded: Set<string>): string {
    const { id, name, hasChildren } = node.data;
    if (!hasChildren) return name;
    const glyph = expanded.has(id) ? '⊖' : '⊕';
    return isSource(node) ? `${glyph}  ${name}` : `${name}  ${glyph}`;
}

/** Nodes are named directly; a link reads as the flow between its two endpoints. */
function describe(datum: LayoutNode | SankeyLink<FlowNode, FlowLink>): string {
    return datum.kind === 'node'
        ? datum.data.name
        : `${datum.sourceNode.data.name} → ${datum.targetNode.data.name}`;
}
