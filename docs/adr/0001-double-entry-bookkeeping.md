# Double-entry bookkeeping as the ledger foundation

The ledger is modelled as classical double-entry bookkeeping: a `JournalEntry` owns two or more `JournalLines` whose signed amounts net to zero, each line referencing exactly one `Account` of type `Asset`, `Liability`, `Equity`, `Income`, or `Expense`. We chose this over the YNAB-style single-entry + category model because it makes transfers, refunds, splits, and (eventually) multi-currency fall out of one consistent shape rather than requiring special cases, and because it lets budgets, labels, and reporting be layered *on top* of a correct ledger later instead of being entangled with it.

Note: counterparties (e.g. "Albert Heijn") are *not* modelled as accounts (an explicit departure from Firefly III). They are separate metadata on a `JournalEntry`. See `CONTEXT.md` for the terminology.
