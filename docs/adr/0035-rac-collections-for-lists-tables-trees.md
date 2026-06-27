# 0035 — React Aria collections for lists, tables, and trees

## Status

Accepted (2026-06-27)

## Context

ADR-0024 adopted react-aria-components (RAC) for the SPA's *input* and *overlay*
layer but explicitly scoped out the **non-input structural** surfaces: "Row-selection
lists (inbox/register div-grids with shift-click ranges) are explicitly out of scope;
converting them to RAC `GridList`/`Table` is a follow-up slice." This ADR is that
follow-up.

An audit of the structural surfaces found they are all hand-rolled with `<div>`
grids, native `<table>`, recursive `<div>` trees, and `role="tab"`/`title=`
attributes. Two of these carry real hand-written machinery that RAC ships natively:

- **Register** (`AccountDetail`) hand-rolls a select-all checkbox with
  `none/some/all` indeterminate state, `toggleRow`/`toggleAll`, page-bound
  selection pruning, and "only Uncleared rows selectable".
- **Bank transaction inbox** hand-rolls all of the above *plus*
  `computeRangeSelection` (anchor-tracked shift-click ranges) and the
  `selectAllVisible`/`clearVisibleSelection`/`allVisibleSelectionState` family.

The account hierarchy is rendered twice (the `Accounts` screen and the sidebar) as
recursive `<div>`s with no `role="treeitem"`/`aria-level`/set-size. The
`BankAccounts` owner filter uses `role="tablist"`/`role="tab"` with no tab panels
behind it — an a11y lie that also lacks the tab keyboard model.

## Decision

Adopt RAC's collection components for **interactive** non-input structures. The
discriminator between the table-shaped components is **cell content, not selection**
(RAC `Table` *and* `GridList` both ship `selectionMode="multiple"`, select-all, and
shift/keyboard range selection):

- **`Table`** — rows of scalar cells you scan, compare, or sort down a column, with
  a column header. → `Currencies`, `Loans` (header once, row `href`, two tables for
  active/ended), `AccountDetail` register, `Activity` (read-only).
- **`GridList`** — a list of composite, multi-control, or inline-editable rows (no
  scalar-cell grid). → bank transaction inbox, plus the `Tokens`/`Users`/
  `Counterparties`/`BankImports` record lists.
- **`Tree`** — the chart-of-accounts hierarchy on the `Accounts` screen and the
  sidebar. Shared tree scaffolding + grouping helpers (`buildChildrenMap`,
  `groupRootsByType`), swappable row content via render-prop. Type grouping stays
  *outside* the tree (one `<Tree>` per type section), since RAC `Tree` has no
  `Section` concept.

The register and inbox go **all-in on RAC's selection model**: `selectedKeys` +
`disabledKeys` replace the hand-rolled selection state, deleting
`computeRangeSelection`, the `*VisibleSelection*` family, `HeaderSelectAllCheckbox`,
and the `none/some/all` plumbing. RAC's selection manager covers shift-click and
shift+arrow ranges; cleared/non-`Uncleared` rows become `disabledKeys` with
`disabledBehavior="selection"`.

The `BankAccounts` owner filter drops the fake `role="tab"` markup for the existing
`ToggleButtonGroup` (single selection). RAC `Tooltip` is adopted only for hints on
focusable controls; truncation reveals keep `title=` with the full text in the DOM
(RAC `Tooltip` requires a focusable trigger and is not shown on touch).

### Exceptions

- **`LoanScheduleTable` stays a native `<table>`.** Its grouped, two-row column
  headers (each loan part spans three sub-columns via `colSpan`) and nested
  year→month expandable rows are shapes RAC `Table`'s column model can't express
  without UNSTABLE expandable-rows APIs and column-group hacks.
- **The sidebar's flat nav** (`NAV_MAIN`/`NAV_OTHER`) stays plain `<nav>`/`<Link>` —
  it is flat navigation, not a collection, and gains nothing from a RAC wrapper.
- `Pagination` and purely presentational components remain custom (unchanged from
  ADR-0024).

## Consequences

- The register and inbox selection state machines are deleted, not maintained;
  keyboard nav, focus, range selection, and ARIA grid/list roles come from RAC.
- The account tree becomes accessible (`role=treeitem`, `aria-level`, set-size,
  arrow-key expand/collapse) and collapsible on both surfaces. Arrow-key nav is
  per-type-section (one `<Tree>` each), not one continuous ring — a net gain over
  today's zero tree-keyboard support.
- The `BankAccounts` filter stops announcing non-existent tabs.
- `Table` selection and `GridList` keyboard models make the inbox row the primary
  focus stop, with inline editors as secondary tab stops within the focused row —
  accepted as the trade for deleting the custom selection code.
