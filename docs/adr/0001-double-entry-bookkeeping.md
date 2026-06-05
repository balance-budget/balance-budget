# Double-entry bookkeeping as the ledger foundation

The ledger uses classical double-entry bookkeeping: a `JournalEntry` owns two or more `JournalLines` whose signed amounts net to zero, each line referencing exactly one `Account` of type `Asset`, `Liability`, `Equity`, `Income`, or `Expense`.

We chose this over a YNAB-style single-entry + category model because transfers, refunds, splits, and eventual multi-currency all fall out of one consistent shape, and budgets, labels, and reporting can layer on top of a correct ledger rather than being entangled with it.

Counterparties (e.g. "Albert Heijn") are not Accounts — they are separate metadata on a `JournalEntry` (a deliberate departure from Firefly III). See `CONTEXT.md` for terminology.
