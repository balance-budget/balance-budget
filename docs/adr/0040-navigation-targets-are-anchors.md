---
status: accepted (extends ADR-0035)
---

# Anything that navigates is an anchor

An SPA that hijacks navigation with click handlers breaks the browser. Until now most
of the app's navigable surfaces were not links: the sidebar account tree, all six
row-clickable collections (`Loans`, `Accounts`, `Activity`, the register,
`Counterparties`, `CounterpartyDetail`, `BankAccounts`), pagination, and the launcher
all navigated from an `onRowAction`/`onAction` callback calling `navigate(...)`. None
of them could be opened in a new tab from the context menu, middle-clicked,
cmd-clicked, or previewed in the status bar.

## The line

**A control whose effect is to navigate to a different resource renders an `<a href>`
with the real URL.** Rows, tree rows, pagination, breadcrumbs, launcher results, and
"go to X" affordances are links.

Controls that mutate server state, open a dialog, or refine the current view stay
buttons — *even when they write the URL*. Search fields, filters, sort headers, and
expand/collapse chevrons all encode themselves in search params and are still not
links: they are form controls over the resource you are already on. Redirects that
follow a mutation (create an entry, delete a loan) stay imperative `navigate` calls;
there is no control to click.

Pagination sits on the navigating side of the line despite addressing the same
resource, because "open page 3 in a new tab" is a thing people do.

## `href` is a `ToOptions` object, not a string

React Aria's `RouterProvider` is wired to the TanStack router in `main.tsx`, and RAC's
`RouterConfig` interface is augmented so `href` takes TanStack's `ToOptions`:

```tsx
<Row href={{ to: '/journal/$id', params: { id } }} />
```

Route, params, and search stay type-checked, which a URL string would not be. The
adapter (`lib/router.ts`) resolves the object with `router.buildLocation(...)` for the
DOM attribute and `router.navigate(...)` for the click.

## The anchor lives in the row-header cell

React Aria renders `Table` rows, `GridList` items, and `Tree` items as `role="row"`
elements carrying `data-href`, and synthesizes a throwaway `<a>` at press time
(`useTableRow`/`useGridListItem` → `useSyntheticLinkProps`). That is enough for
cmd-click and middle-click, but a right-click context menu and the status-bar preview
need an anchor that is actually in the DOM. `ListBox`, `Menu`, and `Tabs` items *do*
render as real anchors when given an `href`, so they need nothing extra.

So a navigating row carries both: `href` on the row (whole-row click, keyboard
activation, modifier keys) and a `RowLink` wrapping the **row-header cell's** content
(the browser's link affordances). One anchor per row, pointing at the row's own
destination; a row may hold further anchors only when they point somewhere *else*.

Rejected: a stretched overlay anchor covering the whole row. It would make right-click
work anywhere in the row, but it fights RAC's press handling, swallows text selection,
and cannot contain the tree's expand button (a `<button>` inside an `<a>` is invalid).
Users right-click the name, not the whitespace.

This forced two row headers to move from the date to the description (`Activity` and
the register): the date is not what identifies a journal entry, and it made a poor
link target and a poor screen-reader row name.

## Enforcement

`components/ui`'s `Table`, `GridList`, and `Tree` wrappers omit `onRowAction`/
`onAction` from their prop types, so navigating by callback is unspellable and `href`
is the only way. `ListBox`/`Menu` keep `onAction` — they legitimately run commands as
well as navigate — and rely on this convention plus review.

Link targets are written inline as `ToOptions` at the call site. Only targets whose
route requires search defaults are extracted (`accountRegisterLink`), because those
are the ones that must stay in sync with a `validateSearch`. A central `links.ts`
would only hide TanStack's own typing.
