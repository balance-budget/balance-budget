# Money stored as `long` minor units + currency code

Monetary amounts are stored as `(Amount: long, CurrencyCode: string)`, where `Amount` is the value in the currency's smallest unit (cents for EUR/USD, yen for JPY, satoshi for BTC, …) and the currency's `MinorUnitScale` lives in a reference table. We rejected `decimal(19,4)` because (a) SQLite has no native decimal type and stores it as TEXT, which silently degrades to IEEE-754 doubles for any server-side arithmetic — a permanent footgun across raw SQL aggregates and equality predicates; (b) a fixed (19,4) scale breaks for crypto currencies later; and (c) `bigint` arithmetic is bit-exact on both SQLite and Postgres, making the per-entry zero-sum invariant trivially provable.

Amounts are wrapped in a `Money` value object so same-currency arithmetic is type-checked and cross-currency arithmetic is a compile error. Sign convention on `JournalLine.Amount`: positive = debit, negative = credit (Beancount/Ledger-CLI convention).
