---
status: accepted
---

# Bank-statement import: shape, metadata, and importer dispatch

Importing a bank statement consumes a CSV stream against a user-chosen `BankAccount`, validates it, and records immutable `BankTransaction`s only — no `JournalEntry` is auto-created at import time, keeping "what the bank told us" separate from "what we did about it" (ADR-0003); categorization is a later flow (ADR-0013) and inbox exit states are owned by ADR-0012. The single import route is `POST /api/bank-accounts/{id}/imports` (`multipart/form-data`), with all per-bank parsing behind the integration layer (ADR-0018).

## Transaction shape and dedup

A `BankTransaction` is immutable in its bank-supplied fields and carries the universal columns `Description`, `CounterpartyName?`, `CounterpartyAccountNumber?` (every statement row across every bank has these), plus `RawSource` (the original row text) and `RowHash` — a SHA-256 hex digest computed over the normalized raw row bytes (CRLF `\r\n` normalized to `\n`, trailing whitespace stripped). Dedup is per-`BankAccount`: the unique index is `(BankAccountId, RowHash)`, so identical-looking fee rows on two separate accounts stay distinct while the index is the durable backstop behind in-memory dedup. Hashing raw bytes rather than parsed fields keeps the hash a stable property of what the bank said, independent of parser version, so re-extraction stays idempotent. Malformed rows fail-fast and roll back the whole import.

## Promoted SEPA fields and metadata

A fixed bank-agnostic SEPA / ISO-20022 column set is promoted onto `BankTransaction` — `ValueDate`, `Reference`, `MandateId`, `SepaCreditorId`, `ForeignAmount`, `ForeignCurrencyCode`, `ExchangeRate` — because these recur across European exports and are worth scanning and matching on as first-class columns. Everything else an extractor parses lives in the normalized key-value `BankTransactionMetadata` side-table, rebuilt wholesale by re-extracting from `RawSource` (the only post-import-mutable surface besides the user `Dismissed*` fields). Metadata keys are a single global namespace — bank-agnostic where the concept is shared, bank-prefixed (e.g. `IngTransactionCode`) only for genuinely bank-specific extras — so cross-bank queries match without first knowing which extractor produced the row.

## Importer dispatch by account type

`BankAccount` discriminates by a `BankAccountType` enum (`Current`, `Savings`, `Card`) and carries an `ImporterKey` naming the extractor for its imports. Each `IBankTransactionExtractor` declares a `Key` and a `SupportedType`; the service resolves the extractor whose `Key` equals the BankAccount's `ImporterKey`, and at both write-time and dispatch the chosen extractor's `SupportedType` must equal the BankAccount's `BankAccountType`, so the database never holds an incoherent pair. A `Card` BankAccount must be owned by an Account (`AccountId IS NOT NULL`) and is matched by its normalized `CardIdentifier`; the BankAccount currency requirement is unchanged (ADR-0010).
