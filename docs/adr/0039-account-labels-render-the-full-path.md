---
status: accepted (amends ADR-0011; extends ADR-0019, ADR-0035)
---

# Account labels render the full path, and `›` never means money flow

[ADR-0019](0019-nested-accounts.md) made **Accounts** a tree, which made a bare leaf name
ambiguous: `Tax` under `Car` and `Tax` under `Home` render identically. The account
*pickers* solved this immediately (`AccountSelect` shows `5110  Car › Tax`), but every
read-only display kept rendering the flat `AccountName` the API sends — the register,
journal lines, the Activity list, search results, the loan panels. So the one place you
choose an account tells you where it sits, and the places you *read* it back do not.

## One component, fed from the accounts cache

Every read-only account display goes through **one** component, `AccountLabel`, which
takes an `accountId` and resolves the path itself. Call sites pass an id, never a
pre-built list or map — the same rule that keeps the pickers from drifting (ADR-0019).

The path is built **client-side** from the `useAccounts()` cache, not supplied by the
server. `GET /api/accounts` returns the whole chart of accounts unpaged, and the sidebar
in `AppShell` loads it on every screen, so the data is already in memory on every page
that would render a label; the walk is a handful of `Map` lookups per row over a
few hundred accounts. This is why there is **no materialized `Path` column** on
`Account`: a denormalized path is the only variant that can go stale, and re-parenting a
node would have to rewrite every descendant's path to keep it honest. Renaming or
re-parenting an account instead updates every label in the app as soon as the accounts
query invalidates.

Read models keep their flat `AccountName` field. It is the **fallback**: `AccountLabel`
renders it verbatim when the accounts cache has not resolved yet or the id is absent
from it, so a row shows a usable name on first paint and upgrades to the path when the
cache lands. Where a read model carried a name but no id (the Attach hints) the id was
added rather than accepting a label that cannot resolve.

## What a label looks like

- **Ancestors dimmed, leaf inherits** the surrounding cell's color, joined by
  `ACCOUNT_PATH_SEPARATOR`. No font-weight change mid-string: a weight shift at
  `text-xs` reads as jitter.
- **No baked-in font size.** Cells set `text-xs`/`text-sm` themselves and the label
  inherits, so one component works in a dense register row and a detail panel.
- **The `Code` is opt-in** (`showCode`), off by default. In a picker the code is
  load-bearing — you type `5131` to jump to an account. In a 200px table cell it costs
  a third of the width the path is already starving for.
- **A glyph leads the label**, in three variants: `icon` (a 20px `xs` `AccountAvatar`),
  `dot` (a 6px circle in the AccountType accent), or `none`. The icon carries the
  account's identity, since it is the one visual the user chooses per account; the color
  never is — it always derives from the AccountType (see **Account icon** in
  `CONTEXT.md`). A `dot` therefore conveys *type only*, which is why it is the variant
  for columns where the icon would repeat down every row (a register's posted-account
  column, all of whose rows are descendants of the account being viewed), not a
  general-purpose default.

## Ancestors give up their pixels first

A path is truncated **from the middle-left, never the right**. Plain `truncate` cuts the
tail, which eats the leaf — the single most identifying segment — and yields
`Car › Insurance › Ex…`. Instead the ancestors carry `shrink-[9999]` and the leaf the
default shrink factor, so flexbox spends the ancestors' width down to `…` before the
leaf loses a pixel, yielding `Car › Insu… › Excess`. The separator before the leaf is a
`shrink-0` element of its own, otherwise the ellipsis swallows it. A very long leaf in a
very narrow cell still truncates, as a last resort.

This is CSS, not measurement: it adapts to the actual column width and to viewport
resizes for free. The rejected alternative — precomputing a middle collapse
(`Car › … › Excess`) past a depth threshold — discards ancestors even when the column
has room and needs a threshold tuned per call site.

## Truncation reveals stay `title=`

[ADR-0035](0035-rac-collections-for-lists-tables-trees.md) ruled that RAC `Tooltip` is
for hints on focusable controls and truncation reveals keep `title=`. That holds, and the
arithmetic is the reason: RAC `TooltipTrigger` requires a focusable trigger, so a styled
tooltip on register labels means wrapping ~100 labels per page (50 rows × 2 account
columns) in focusable elements — tripling the table's tab stops — and a focusable child
inside a row whose `onRowAction` navigates elsewhere creates a click ambiguity. The
payoff is small: CSS ellipsis truncates visually only, so the full path is in the DOM and
assistive tech reads it either way, and RAC tooltips do not fire on touch. The gain would
be prettier styling for sighted pointer users.

So: `title=` with the untruncated path in dense collections, RAC `Tooltip` only where a
focusable trigger already exists for its own reasons (the breadcrumb's links).

## `›` descends the tree; `→` moves money

The account path separator and the journal from→to indicator had both drifted onto
chevrons, so `Checking › Groceries` could mean either "Groceries is a child of Checking"
or "money went from Checking to Groceries". They now split cleanly:

- **`›`** (and `chevron-right` as an affordance) means **descend a level** — an account
  path, a page breadcrumb, a drill-down, a disclosure twisty.
- **`→`** (`arrow-right`) means **money moved from here to there**, and nothing else.

## From and To become separate columns

The Activity and Counterparty registers rendered both sides of an entry in one 220px
cell, where two paths cannot both fit. They split into two independently-truncating
columns, matching the widths of the AccountDetail register's Account and Counter columns
so the tables scan alike. The headers stay **From / To**, not Account / Counter: the
AccountDetail register is per-**line** and focal-account-relative, while these are
per-**entry** with no focal account, so their two sides are the credit and debit sides.
`JournalDetail`'s one-line entry summary keeps a single `→` — it is a sentence, not a row.

This **amends [ADR-0011](0011-journal-overview-amount-sign.md)**: `fromLegs` / `toLegs`
are now **always** populated, not only when `isSimplifiable`. The `Split (N lines)`
blackout existed because a single `A → B` arrow lies about an N-to-M entry; with two
independent columns there is no arrow to lie, so an N-to-M entry lists its credit
accounts under From and its debit accounts under To (each `first +N`). `isSimplifiable`
survives for the consumers that still render an arrow.

## Consequences

- The chart of accounts must stay small enough to ship unpaged and hold in memory. It is
  a per-user chart of accounts, so this is a safe assumption; if `GET /api/accounts` ever
  paginates, `AccountLabel` degrades to `fallbackName` rather than breaking, but the
  decision above would need revisiting.
- Deep paths in narrow columns rely on hover to be read in full, and hover does not exist
  on touch. Accepted: the leaf, the segment that identifies the account, is the one part
  truncation never takes.
- `AccountLabel` renders in every row of every collection, so it must stay cheap. The
  id→account index is memoized on the accounts array identity (a module-level `WeakMap`),
  not per component instance, so a hundred labels in one table share one index.
