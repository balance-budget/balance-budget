---
status: accepted
---

# Logical importers, content-sniffed statement layouts, and drop-and-detect import

This revises the importer model of [ADR-0009](0009-bank-statement-import.md). A **BankAccount** now binds to a *logical* **Importer** — a (bank, `BankAccountType`) identity keyed `Ing.CurrentAccount` / `Ing.SavingsAccount` / `Ing.CreditCard`, with the `.Vn` version suffix dropped from every stored `ImporterKey`. The concrete **Statement layout** (e.g. ING's pre-2016 vs current credit-card PDF) is resolved *per file by content sniffing*, never bound to the account, because one real-world account spans format eras over its life. The two ING credit-card extractors (`.V1`/`.V2`) merge into one `Ing.CreditCard` importer that sniffs the layout internally; a data migration rewrites existing `ImporterKey`s (`Ing.*.V1`, `Ing.CreditCard.V2`) on both `BankAccounts` and `BankTransactions` to their logical form. Re-extraction re-sniffs the layout from `RawSource`, so the stamped key stays uniformly logical.

On top of this, a **Statement detection** flow lets the user drop files with no account chosen. Each `IBankTransactionExtractor` gains a `TryIdentifyAsync(ImportFile { FileName, Stream })` probe that returns an **Account anchor** (IBAN/`AccountNumber`/`CardIdentifier`) without a target account — taking the filename as a fast path where present (ING current/savings) and falling back to content sniffing otherwise. A bank-agnostic resolver maps the anchor to a `BankAccount`. The hard invariant: **detection only proposes a target; the existing `ExtractAsync` re-validates the content anchor and a mismatch is a hard failure**, so a renamed or mis-detected file fails loudly and can never write to the wrong account. Only an unambiguous single own-account match imports automatically (`POST /api/imports`, multipart, N files, detect + import-confident); no/ambiguous/non-importable/unrecognized cases are surfaced for manual resolution via the existing `POST /api/bank-accounts/{id}/imports` route. `AccountNumber` and `CardIdentifier` gain unique filtered indexes (scoped to own accounts) so ambiguous matches are structurally impossible, matching the existing `Iban` index.

The friendly importer label is composed in the frontend (`BankName` proper noun from the extractor + the `BankAccountType` word via Lingui), not stored as a `DisplayName` in C#, keeping UI copy translatable per [ADR-0022](0022-frontend-i18n-language-and-region-formatting.md).

## Consequences

- Detection always re-parses on re-extraction (layout is never cached on the row); a future layout change that makes an old `RawSource` un-sniffable surfaces as a loud re-extraction failure, which is treated as correct.
- The drop-zone is the primary import surface; the per-account list is retained below as the explicit path and the manual-resolution target.
