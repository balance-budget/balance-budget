# Money flow: signed Net-movement model

The **Money flow** Sankey (`/api/reports/flow`) is built from one number per postable **Account** — its **Net movement** over the **Reporting period** — not by tracing money paths. Each Account contributes one flow of width `|Net movement|`, placed by sign under the **Sign convention** (ADR-0011): money *in* on the source side (**Income**, plus balance-sheet accounts that shrank) and money *out* on the exit side (**Expense**, plus balance-sheet accounts that grew). All sources converge into a single hub and fan back out to all exits; a net-negative account flips sides for that period.

The diagram balances exactly with no fudge node because of the double-entry identity `Σ Income − Σ Expense = Σ (balance-sheet Net movement)` — the surplus/deficit is just where the balance sheet moved, decomposed by account. This invariant is what tests assert; if it fails, the bug is in Net-movement computation, not the layout. Income/Expense render at full Chart-of-accounts depth; balance-sheet accounts render top-level only in v1, because a mixed-sign subtree (Checking drained, Brokerage grew) cannot sit on both sides at once.

We rejected ledger-true flow tracing (forces proportional split-attribution and implies an income→expense pairing that money's fungibility makes false — manufactured precision, reserved as a future advanced view).
