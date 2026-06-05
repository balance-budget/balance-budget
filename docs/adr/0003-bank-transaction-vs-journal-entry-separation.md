# `BankTransaction` (import fact) is separate from `JournalEntry` (interpretation)

Imported bank-statement rows live in an immutable `BankTransaction` table — deduplicated by hash, carrying the raw amount, dates, description, and `FromBankAccountId` / `ToBankAccountId`. The ledger interpretation lives in `JournalEntry` (with its `JournalLines`), which references its source via `JournalEntry.BankTransactionId?` when derived from an import, or leaves it null for cash entries.

We chose this split — standard in professional bookkeeping (SAP "Document" → "Journal Entry"), uncommon in personal-finance apps — because the hash makes re-imports idempotent, users can revise/split/merge entries without losing the original bank record, and "what the bank reported" stays cleanly separated from "what we did about it". The name `BankTransaction` (ISO 20022 vocabulary) avoids collision with database transactions.
