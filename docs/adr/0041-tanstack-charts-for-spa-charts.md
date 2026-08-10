---
status: accepted (supersedes the charting choice in ADR-0005)
---

# 0041 — TanStack Charts for the SPA charts

## Status

Accepted (2026-08-16). Supersedes the Recharts decision in
[ADR-0005](0005-frontend-stack.md); the rest of that ADR stands.

## Context

Every chart in the SPA was a Recharts composition: the dashboard net worth and
balance trend, the reports money flow and distribution, the register summary,
the loan balance and payment views, and the outlook projection.

Recharts models a chart as a chart type plus typed child components (`AreaChart`
with `Area`, `PieChart` with `Pie`, `Sankey` with a custom node renderer). Charts
that were not one of those types had to reach past the component API: a hand
drawn SVG zigzag positioned through `usePlotArea`, a gradient whose stops were
computed from a hardcoded plot height, a Sankey node renderer reimplemented as a
raw `<g>`, and a local module that computed y-axis domains and ticks because
`'auto'` hugged the data.

TanStack Charts is a grammar of graphics: marks, channels, scales, and guides
compose into one definition. The compositions the app already needed are ordinary
marks (`areaY` with explicit endpoints for the projection cone, `pie` and
`radialArc` for the donut, `sankeyDiagram` for the money flow, `ruleX`/`ruleY` for
markers), and the parts that were hand-rolled are library concerns: responsive
sizing, automatic guide margins, tick thinning, portaled tooltips, keyboard
focus, and reduced-motion-aware animation.

## Decision

Charts are built with `@tanstack/charts` and its React adapter. Recharts is
removed.

- Definitions are memoized `defineChart({ marks, x, y })` values. Marks carry
  their own data; wide application rows are folded into the long rows a mark
  needs.
- `components/Chart.tsx` wraps the adapter's `Chart` and supplies the shared
  defaults: the native tooltip extension (portaled), a tooltip body rendered
  through the existing `ChartTooltip` chrome, and money formatting. It adds
  defaults only; every adapter prop passes through.
- Scales come from the compact entries: `scalePoint`/`scaleBand` over ISO date
  strings, `scaleLinear` with `nice: true` for money. Dates stay categorical
  because the buckets are dense and evenly spaced, which is what the library
  documents compact scales for. `d3-shape` is a direct dependency for
  `curveMonotoneX`.
- Series color runs through the chart color scale (`color` channel plus a
  `domain`/`range` built from the app palette), so legends and tooltip swatches
  resolve from one source.
- Legends are `colorLegend`, or `interactiveColorLegend` driven by a
  `controlledSignal` when the screen owns series visibility.
- Axis domains, ticks, and margins are the library's. The app no longer computes
  them.

## Consequences

Charts are shorter and describe what they mean rather than which component
renders them. Tooltips, keyboard focus, and reduced motion are consistent across
all seven charts without per-chart work.

Three visual behaviors changed:

- The broken-axis indicator is gone. Axes are data-driven and niced; there is no
  truncation cue.
- The projection band is a flat translucent fill instead of a vertically fading
  gradient.
- Reference lines (`Year-end`, `today`, rate-fixation boundaries) draw without
  their text labels. Rules carry no data, so they emit no interaction points and
  cannot label themselves through the tooltip.

The outlook projection axis now includes zero, because its zero reference rule
contributes to the y domain. A balance far above zero therefore reads flatter
than it did.

Charts have no automated tests. `moneyAxis` and its unit tests were deleted with
the axis math they covered.
