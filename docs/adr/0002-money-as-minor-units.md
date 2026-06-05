# Money stored as `long` minor units + currency code

Monetary amounts are stored as `(Amount: long, CurrencyCode: string)`, where `Amount` is the value in the currency's smallest unit (cents for EUR/USD, yen for JPY, satoshi for BTC), and each currency's `MinorUnitScale` lives in a reference table. Amounts are wrapped in a `Money` value object so same-currency arithmetic is type-checked and cross-currency arithmetic is a compile error.

Sign convention on `JournalLine.Amount`: positive = debit, negative = credit (Beancount/Ledger-CLI convention).

We rejected `decimal(19,4)` because SQLite has no native decimal type and silently degrades it to IEEE-754 doubles for server-side arithmetic, and because a fixed (19,4) scale breaks for crypto. `bigint` arithmetic is bit-exact on both SQLite and Postgres, making the per-entry zero-sum invariant trivially provable.
