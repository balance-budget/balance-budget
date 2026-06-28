# Sidebar account-tree depth shading

## Context

The sidebar chart-of-accounts moved to a React Aria `Tree` (ADR-0035), whose
rendered DOM is flat: rows are siblings with `aria-level`, not physically nested
containers. The previous look (see `sidebar.png`) relied on nested containers:
each level sat on a slightly lifted surface, the first/last row of a sibling
group had rounded top/bottom corners, and the area outside those rounded corners
showed the parent level's surface. We need to reproduce that look without nested
DOM.

## Decision

Each account row paints **three opaque, full-width, non-inset layers**, outer to
inner:

1. **outer** = the parent level's band color. Square, except it rounds its
   **bottom** when the parent band also ends at this row.
2. **band** = the row's own level color. Rounds its **top** when the row is the
   first child of its parent, and its **bottom** when it is a last leaf /
   collapsed last child (the last-descendant of the band; flat DOM forces
   bottom-rounding onto the deepest last leaf, not the parent row). Those
   rounded gutters reveal the **outer** (parent) shade.
3. **content** = the row itself, a fully-rounded pill. Transparent by default so
   the band shows through; fills with `brand-primary-soft` + `text-brand-primary`
   when active, or the one-step-brighter hover/focus tint on
   `hover` / `focus-visible`. Its rounded gutters reveal the **band** shade.

The third layer is what lets a highlighted row (active or hovered) still close a
band correctly: the band layer reveals the parent at its rounded corners while
the pill reveals the band at its own corners. Level-1 rows have transparent
outer and band layers, so their hover/active pill simply floats on the sidebar
base.

Band colors come from a **4-stop opaque ramp** (L1 = sidebar base / no fill;
L2-L4 increasing, brighter in dark and darker in light per ADR-0031); levels
deeper than L4 **clamp** to the deepest stop. Level-1 rows (type roots) get no
fill and no rounding.

## Why opaque, and the two consequences

Opaque colors were chosen over the old translucent alpha lift to drop the
layer-compositing math: with translucent levels, reproducing the nested look
needs one composited layer per ancestor level and per-level increment
arithmetic. Opaque colors make each level "just this color," so a fixed three
layers (parent + own + highlight pill) reproduce every case in the target
screenshot regardless of depth.

Two accepted trade-offs:

1. Nested rows no longer share the sidebar's translucent `backdrop-blur`; they
   are solid. Invisible in practice deep in the nav.
2. A single leaf that closes two or more bands at once (last child of a last
   child) would need an extra layer per closed band to show every intermediate
   shade in its corner gutter; the fixed three layers show the immediate parent
   and then fall back to the base sidebar color. It is a 1px sliver and the
   common case (a sibling separates the groups, as in the screenshot) is exact.
