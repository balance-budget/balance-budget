---
status: accepted
---

# A Card BankAccount names its Funding account, and the ING credit-card CSV is a third layout

ING now exports credit-card statements as CSV alongside the PDF. Per [ADR-0034](0034-logical-importers-and-drop-and-detect-import.md) the CSV is a third **Statement layout** under the existing `Ing.CreditCard` **Importer**, not a new importer — a single card spans format eras, and the era must not be welded to the account. Consequently `IIngCreditCardStatementParser` moves from PDF text lines (`CanParse(IReadOnlyList<string>)`) to a stream (`CanParseAsync(Stream)`), with the two PDF layouts sharing a base that does the `LooksLikePdf` + line-extraction step.

The CSV omits two things the PDF states, and both omissions are load-bearing.

## Funding account

The PDF prints the current account that settles the card, which the extractor uses to populate `CounterpartyAccountNumber` on pay-down rows so **Attach** can pair them with the current-account leg. The CSV prints nothing, and rows that move money between the card and that account carry **no card number and no counterparty** — the blank card number *is* the marker that the row is such a transfer. So the link becomes configuration: an optional `BankAccount.FundingBankAccountId` self-reference on the `Card` side (see **Funding account** in `CONTEXT.md`). This is a new pattern — no `BankAccount` referenced another before — justified because it names a real-world fact (this card settles against that account) that the bank omits precisely because a human knows it implicitly, the same way a savings account's tie to a current account is implicit.

The configured link **wins** over the value scraped from a PDF, and a disagreement between the two is a hard `ImportIbanMismatch`, consistent with ADR-0034's rule that content contradicting the target fails loudly. Scoped by the FK's optionality: the check only fires when a Funding account is configured, so cards without one keep the existing PDF behavior. The accepted cost is that a card whose funding account changed over its life cannot re-import its older PDFs until the FK is corrected or cleared — preferred over silently ignoring one of two disagreeing sources.

## Dedup across PDF and CSV

Dedup is `(BankAccountId, RowHash)` over raw row text (ADR-0009), and the hash basis is per-account, not per-layout — so the same transaction imported once as PDF and once as CSV yields two rows. We deliberately **do not** reconcile across layouts: no parsed-field fingerprinting, no fuzzy matching. The user cuts over from PDF to CSV at a period boundary. Rejected alternatives were hashing parsed fields (breaks ADR-0009's parser-version-independent hash and re-extraction idempotency) and reporting suspected duplicates from a fingerprint multiset (real, but a cross-cutting feature that this change doesn't need).

Within a *single* CSV file the risk is real rather than theoretical, because the card CSV has no running-balance column to make rows accidentally unique — two identical charges on one day produce byte-identical rows, and the second would be silently swallowed as a duplicate. The CSV layout therefore prefixes the second and later occurrences of an identical row with `N|` (`2|"2026-01-03";"DL *Taxi..."`). The prefix goes into **both** `RawSource` and the hash basis, so a hash remains recomputable from what we stored; first occurrences are unprefixed, so an ordinary row stores exactly the bank's line. A quoted CSV row always starts with `"`, so the prefix can never be confused with bank content. Scoped to the CSV layout: the PDF layouts' hashes are left untouched so no stored hash moves.

## Dialects, not layouts

The Dutch and English CSV exports differ in header names, number culture (`1.483,18` vs `1,483.18`, and a `Koers` of `0,0000489` that nl-NL parsing turns into `489`), note vocabulary, and note date format (`dd-MM-yyyy` vs `dd/MM/yyyy`). These are one layout with a **dialect** detected from the header row, not two layouts — layouts are reserved for format eras. The English vocabulary is lossy: `Payment` covers both `Incasso` and `Ontvangst`, disambiguated by whether the row carries a card number. Dutch is treated as the authoritative vocabulary; unrecognized labels map to `Unknown` rather than throwing, since ING demonstrably adds labels over time (`Diversen` is new).
