# Personal Finance Bookkeeping

A personal-finance tool backed by a rigorous **double-entry ledger**. The domain is the bookkeeping core — accounts, journal entries, postings. Budgets, labels, imports, and reporting are deliberately deferred and will sit *on top* of this ledger later.

## Language

**Account**:
A ledger account in the double-entry accounting sense (e.g. "Groceries Expense", "ABN AMRO Checking", "Visa Credit Card"). Every Account has exactly one **AccountType**.
_Avoid_: bucket, category, envelope, payee (those are different concepts).

**AccountType**:
The accounting classification of an **Account**, one of the five standard types: **Asset**, **Liability**, **Equity**, **Income**, **Expense**. Determines normal balance (debit-normal for Asset/Expense; credit-normal for Liability/Equity/Income) and how the account contributes to reports.

**Asset**:
An **Account** representing something owned or money owed to you. Examples: ABN AMRO Checking, Cash, Savings, Investments, "Owed by Alice" (informal receivable).

**Liability**:
An **Account** representing money you owe. Examples: Visa Credit Card (yes — a credit card is a liability, not an asset), Mortgage, Personal Loan, "Owed to Bob" (informal payable).

**Equity**:
An **Account** representing net worth or capital. In personal-finance use, primarily holds opening balances when accounts are onboarded with non-zero starting balances. The seeded `Opening Balances` **Equity** account is the canonical home for these.

**Opening balance**:
The starting balance of an **Asset** or **Liability** **Account** at onboarding. Recorded as a normal **JournalEntry** with one line on the account itself and an offsetting line on the seeded `Opening Balances` **Equity** account. Avoids the Firefly-III hack of using a fake "Initial balance" income account (which permanently pollutes income reports).

**Income**:
An **Account** representing where money comes from. Examples: Salary, Interest Received, Dividends, Cashback. Distinct from **Expense** (not just sign-flipped) so reports can answer "where did money come from?" directly.

**Expense**:
An **Account** representing where money goes. Examples: Groceries, Rent, Utilities, Dining Out. Refunds reduce the expense by being credited on the same **Expense** account (expenses can be credited — it just lowers the balance).

**JournalEntry**:
One bookkeeping event in the double-entry ledger — a header record carrying date, description, and optional **Counterparty**, owning two or more **JournalLines** whose amounts net to zero. The unit of "I bought groceries", "I got paid", "I transferred money".
_Avoid_: Transaction (reserved for the import-side concept — see below), posting, document.

**JournalLine**:
One side of a **JournalEntry** — a signed **Money** amount against exactly one **Account**, with a **ReconciliationStatus**. A **JournalEntry** has at least two **JournalLines**; their amounts must sum to zero (per **Currency**, once multi-currency lands).
_Avoid_: posting, split, entry, line item.

**ReconciliationStatus**:
Per-**JournalLine** state tracking how well a recorded line matches the bank's record. One of `Uncleared` (recorded but not yet seen on a statement — default), `Cleared` (seen on a statement / matched on import), `Reconciled` (explicitly confirmed during a reconciliation pass).

**Money**:
A value object pairing an integer amount of minor units with a **Currency**. Stored as `(Amount: long, CurrencyCode: string)`. Wraps parsing, formatting, and same-currency arithmetic; cross-currency arithmetic is a compile error.
_Avoid_: decimal, BigDecimal, raw long, "amount in cents".

**Currency**:
An ISO-4217 (or ISO-4217-like, for crypto) currency identified by its **CurrencyCode**, carrying a **MinorUnitScale** that determines how many minor units make one major unit.
_Avoid_: "currency type", "denomination".

**MinorUnitScale**:
The exponent for converting a **Money** amount to/from its display value. EUR → 2 (100 minor units = €1.00); JPY → 0; BTC → 8; ETH → 18. The only place rounding happens is at the input boundary when parsing a major-unit value into minor units. Stored as a column on the **Currency** reference table.

**Currency** (entity):
A reference-data row in the `Currency` table: `(Code: string PK, Name: string, MinorUnitScale: int, Symbol?: string)`. Seeded on migration with common ISO 4217 currencies. **Accounts** and **BankAccounts** reference currencies by code (FK). New currencies (incl. crypto) are added by inserting a row, not by changing code.

**Sign convention** (for **JournalLine.Amount**):
Positive = **debit**, negative = **credit**. The zero-sum invariant on a **JournalEntry** is therefore `SUM(Amount) = 0` per **Currency**. Per-**Account** running balance:
- **Asset** / **Expense** (debit-normal): balance = `SUM(Amount)`.
- **Liability** / **Equity** / **Income** (credit-normal): balance = `-SUM(Amount)`.

**Counterparty**:
The real-world party on the other side of a transaction (e.g. "Albert Heijn", "Employer X", a friend you split a bill with). Distinct from **Account** — counterparties are *not* ledger accounts; they are metadata on a **JournalEntry** that records "who".
_Avoid_: payee account, vendor account, expense account (those conflate counterparty with **Account**).

**BankAccount**:
A real-world bank account known to the system, identified by IBAN *and/or* a bank-internal account number. Carries optional `Iban`, optional `AccountNumber`, optional `Bic` / `BankName` / `AccountHolderName`, and a `CurrencyCode` whose nullability depends on ownership (see below) — with a CHECK constraint that at least one of `Iban` or `AccountNumber` is set (a **BankAccount** without any identifier is meaningless). Owned by exactly one of: an **Account** (`BankAccount.AccountId` set — this is one of yours) or a **Counterparty** (`BankAccount.CounterpartyId` set — this belongs to a counterparty). The XOR is enforced as a single-table CHECK constraint. When tied to an **Account**, `CurrencyCode` is **required** — a BankAccount you own must have a known currency so imports and balances stay unambiguous. When tied to a **Counterparty**, `CurrencyCode` is **optional** — a counterparty's currency may be unknown and isn't needed for bookkeeping on your side. This conditional-NOT-NULL is enforced as a single-table CHECK constraint. Used during imports to resolve "the IBAN or account number on the other side of this statement row" to either a self-transfer or a known **Counterparty**.
_Avoid_: bank account details, IBAN entry, payment instrument.

**Register**:
The per-**Account** view of bookkeeping activity — what a banking or accounting UI shows when you "open an account": a chronological list of every **JournalLine** posted to that **Account**, enriched with the **JournalEntry** header (date, description, **Counterparty**) and the offsetting side. *Derived*, not stored — a projection of **JournalLines** filtered to one **Account**. Distinct from a **JournalEntry** (which is the full multi-line bookkeeping event) and from a **BankTransaction** (which is the import-side record). One **JournalEntry** appears in two or more **Registers** — once per **Account** it touches.
_Avoid_: statement (sounds like a printed bank statement — that's a separate concept), ledger (the whole book, not one account), transaction (overloaded — see above), feed.

**RegisterRow**:
One row in a **Register** — derived from exactly one **JournalLine** on the focal **Account**. Carries the focal-account-signed **Money** amount (positive = money in to the focal account, negative = out — *not* the raw debit/credit sign), the **JournalEntry** header (`Date`, `Description`, `CounterpartyId`/`CounterpartyName`), the focal **JournalLine**'s `ReconciliationStatus` and `Description`, and the offsetting side as a list of `(AccountId, AccountName, Amount)` — one entry per non-focal **JournalLine** on the same **JournalEntry**. The list is single-element for a simple two-leg entry and multi-element for a split (e.g. one €100 purchase divided across `Groceries +60` and `Household +40`). UI renders the first element by name and the rest as a "+N" hint; the row's focal amount remains the sum of the focal line(s), so it always matches what a bank statement would show.

**BankTransaction**:
An immutable record of one imported bank-statement row, tied to the user's own **BankAccount** that the row belongs to. Carries the **BookingDate**, signed **Amount** (positive = money in, negative = money out, from the bank's perspective), and **Currency**. The other side of the row is denormalised onto the same record as a free-text `Description` and optional `CounterpartyName` / `CounterpartyAccountNumber` — bank-agnostic fields every statement row carries. In addition, a fixed set of **bank-agnostic SEPA / ISO-20022 fields** is promoted to columns when present on the row: `ValueDate`, `Reference`, `MandateId`, `SepaCreditorId`, and an FX block (`ForeignAmount`, `ForeignCurrencyCode`, `ExchangeRate`). Anything else the extractor parses (ING's transaction code, SEPA creditor name/address, card sequence, FX markup/fee, etc.) lives in a typed **BankTransactionMetadata** key-value blob attached to the row (see [[BankTransactionMetadata]]). The row also carries `ImporterKey` (identifying which extractor produced it — null for manually-created rows), `RawSource` (the original statement-row text as exported by the bank) for audit and re-parsing, and `RowHash` (a content hash of the raw row) for idempotent re-imports. The name matches ISO 20022 vocabulary and is intentionally distinct from a **JournalEntry** — a **BankTransaction** is *what the bank told us*; a **JournalEntry** is *what we did about it*. A **BankTransaction** *may* reference a **JournalEntry** via `BankTransaction.JournalEntryId?` — set when the row is **Categorised** (a new **JournalEntry** is created) or **Attached** (the row is linked to an existing self-transfer **JournalEntry** that the other-side statement already produced); cash **JournalEntries** are not referenced by any **BankTransaction**. A **BankTransaction** may carry user-applied `DismissedAt` / `DismissedReason` metadata recording a **Dismissed** state — these fields, alongside `JournalEntryId` (mutated by **Attach** / **Detach** — see ADR 0013) and the **BankTransactionMetadata** set (which is rebuilt by re-extracting from `RawSource`), are the only mutable surface on the row; all other bank-supplied fields are immutable (see ADR 0010, 0013, and the SEPA-promotion / metadata ADR that supersedes 0010(e)(f)).
_Avoid_: Transaction (overloaded with DB transactions and Plaid/Stripe types), import row, statement line.

**BankTransactionMetadata**:
The set of typed, named extras an **IBankTransactionExtractor** parses out of a statement row that are *not* promoted to columns on **BankTransaction**. Modelled as a key-value side table: every entry is `(BankTransactionId, Key, StringValue | IntegerValue)` where exactly one value column is populated — string for free-text values (e.g. `IngTransactionCode = "IDX"`, `SepaCreditorName = "Vattenfall"`), integer for amounts in minor units or count-like values (e.g. `ForeignMarkUp.Amount = 150`, `CardSequence.Number = 3`). Keys are a global namespace owned across all extractors: bank-agnostic where the concept is shared (`SepaCreditorName`, `OtherParty`), bank-prefixed where genuinely bank-specific (`IngTransactionCode`, `IngMutatiesoort`). Nested values flatten with dotted keys (`ForeignMarkUp.Amount`, `ForeignMarkUp.CurrencyCode`). Distinct from the promoted SEPA / FX columns on **BankTransaction** (which are first-class, indexable, and present on the list view) and from `RawSource` (which is the immutable original-bytes audit trail). **BankTransactionMetadata** is *derived* from `RawSource` by the extractor named in `BankTransaction.ImporterKey` and can be rebuilt from `RawSource` at any time — it is the only field on a **BankTransaction** that an extractor may rewrite for a row that already exists, and the rebuild path is how parser improvements reach historical rows.
_Avoid_: import attributes, bank-row extras, custom fields, properties bag.

**Inbox**:
The derived set of **BankTransactions** that have no referencing **JournalEntry** and no recorded **Dismissed** state — the starting point of the **Categorisation flow**. *Derived*, not stored: the filter is `b.JournalEntryId IS NULL AND b.DismissedAt IS NULL`. Defaulted-sorted oldest-first so the user works the queue in statement order. Each Inbox row carries an optional **Attach hint** (`MatchingJournalEntryId`) when the **Attach predicate** uniquely identifies a self-transfer **JournalEntry** the row should link to (ADR 0013) — surfaces as a one-click `Attach` action alongside `Categorise` and `Dismiss`. Distinct from the full **BankTransaction** list view, which shows every imported row regardless of state.
_Avoid_: queue, unmatched list, pending imports.

**Dismissed**:
A terminal state of a **BankTransaction**, recorded as `DismissedAt` (UTC timestamp) plus `DismissedReason` (short free-text) on the row itself. Used when no **JournalEntry** should ever be created for the row and no existing **JournalEntry** is the right **Attach** target — e.g. a test transaction, a fee corrected elsewhere, a row the user explicitly chooses not to categorise. (The sibling of a self-transfer is handled via **Attach**, not Dismiss — see ADR 0013.) Reversible via undismiss — the row returns to the **Inbox**. User-applied metadata, *not* a mutation of bank-supplied fields; set and cleared only through a dedicated dismiss/undismiss action, never via PATCH or the **Categorisation flow**.
_Avoid_: archived, ignored, deleted (the row still exists and remains immutable in its bank-supplied fields).

**Categorisation flow**:
The user-driven process of producing exactly one **JournalEntry** for one **BankTransaction** — or, for the sibling of a self-transfer, **Attaching** the row to an existing **JournalEntry**. When the BT's `CounterpartyAccountNumber` resolves (via exact match on `BankAccount.Iban`) to one of your own **BankAccounts**, the flow recognises a **self-transfer in progress** and pre-fills the counter-side **Account** with that own-**Account** (leaving `CounterpartyId` null); otherwise the counter-side resolves through an exact-IBAN `Counterparty` match plus a last-used-**Account** heuristic. The new **JournalEntry** is created atomically with the bank-side **JournalLine** in `Cleared` **ReconciliationStatus** and counter-side line(s) in `Uncleared`. The flow also offers a **manual JE-picker** that lets the user attach to an existing **JournalEntry** when the Inbox's strict predicate did not auto-detect a match. Exposed as the composite endpoint `POST /api/bank-transactions/{id}/categorize` (create-new path) and `POST /api/bank-transactions/{id}/attach` (attach path). Multi-Account splits are modelled as multiple **JournalLines** within the single created **JournalEntry**, summing to `−BT.Amount` on the counter side. See ADR 0014.
_Avoid_: import flow (already used for parsing CSVs into BankTransactions — see ADR 0010), assignment, classification.

**Self-transfer**:
A **JournalEntry** that moves money between two of your own **Accounts** — e.g. Current (**Asset**) → Savings (**Asset**), or Current (**Asset**) → Credit Card (**Liability**) when paying down the card. Every **JournalLine** references an **Account** that is yours (`Account` linked via `BankAccount.AccountId`); there is no external party, so `CounterpartyId` is `null`. When both sides of the movement appear as imported **BankTransactions** (one per statement), the first BT to be categorised creates the **JournalEntry** with both lines on the user's own **Accounts** — its bank-side line is `Cleared`, the counter-side line stays `Uncleared` waiting for the sibling. When the sibling BT later imports, the **Inbox** surfaces a one-click **Attach** action that links it to the existing **JournalEntry** and flips the matching line to `Cleared`. A self-transfer **JournalEntry** is therefore referenced by one or two **BankTransactions** over its lifetime, and is the only `JournalEntry` shape allowed to be referenced by more than one. See ADR 0013 for the **Attach predicate** and ADR 0014 for the flow.
_Avoid_: internal transaction, transfer (bare), between-accounts entry. "Transaction" is overloaded; "transfer" alone is ambiguous (some systems use it for any movement, including to a counterparty).

## Relationships

- A **JournalEntry** owns two or more **JournalLines** whose amounts net to zero.
- Each **JournalLine** references exactly one **Account**.
- A **JournalEntry** *may* reference one **Counterparty**; **JournalLines** do not reference **Counterparties**.
- A **Counterparty** is never an **Account** (this is an explicit departure from Firefly III, which models each payee as an expense/revenue account).
- Each **Account** has exactly one **AccountType**.
- Every **JournalLine** carries a **Money** amount; its **Currency** is inherited from its **Account**.
- A **BankAccount** belongs to *exactly one* of: one **Account** (via `BankAccount.AccountId`) or one **Counterparty** (via `BankAccount.CounterpartyId`) — never both, never neither, enforced by CHECK constraint.
- An **Account**-tied **BankAccount** must have a `CurrencyCode`; a **Counterparty**-tied **BankAccount** may leave it null. Enforced by CHECK constraint.
- An **Account** has at most one **BankAccount** (enforced by `UNIQUE(AccountId)` on **BankAccount** where non-null).
- A **JournalEntry** *may* be referenced by one or more **BankTransactions** (when imported) and *may* reference a **Counterparty** (when one is identified). Cash entries have no referencing **BankTransaction** and no (necessarily) **BankAccount**-bearing side; they always have at least a **Counterparty** or a free-text description.
- A **BankTransaction** references at most one **JournalEntry** via the nullable scalar FK `BankTransaction.JournalEntryId`. A **JournalEntry** is referenced by zero, one, or many **BankTransactions**; multiple references are reserved for self-transfers (every line on an own-**Account**, `CounterpartyId IS NULL`) and enforced in the service layer as part of the **Attach predicate** (ADR 0013). Splits are modelled as multiple **JournalLines** within one **JournalEntry**, not as multiple **JournalEntries** sharing a **BankTransaction**.
- A **BankTransaction** is immutable in its bank-supplied fields once stored; the **JournalEntry** referenced by it is editable. The mutable surface on a **BankTransaction** is `JournalEntryId` (via **Attach** / **Detach**), the **Dismissed** metadata (`DismissedAt`, `DismissedReason`), and the **BankTransactionMetadata** set (rebuilt by re-extraction from `RawSource`). Re-imports are deduplicated by hash.

## Example dialogue

> **Dev:** "I bought groceries at Albert Heijn — is Albert Heijn an **Account**?"
> **Domain expert:** "No. Albert Heijn is a **Counterparty**. The **Accounts** involved are 'Groceries Expense' (debited) and your bank account (credited)."

## Flagged ambiguities

- "account" is used in everyday speech to mean both a ledger **Account** and a bank account. Inside the domain, **Account** always means the ledger account (debit-normal or credit-normal, with an **AccountType**); the banking product is a **BankAccount** (carries IBAN / account number / bank metadata). An **Account** may be linked to a **BankAccount** when it represents a real bank product.
- "transaction" is overloaded in everyday speech (DB transactions, bank-statement rows, payment-API events). Inside the domain, the bookkeeping event is a **JournalEntry**; the immutable record of a bank-statement row is a **BankTransaction**. "Transaction" as a bare term is avoided.

## Editing policy (v1)

- **JournalEntries** are editable, with the editable surface gated **per-`JournalLine` by `ReconciliationStatus`** so that observed-against-the-bank state is never silently rewritten:
  - **Entry-level (editable):** `Date`, `Description`, `CounterpartyId`.
  - **BT↔JE link:** managed via `POST /api/bank-transactions/{id}/attach` and `/detach` (ADR 0013) — not via the JE PUT. Attach flips a matching `Uncleared` line to `Cleared`; Detach flips it back. Correcting which JE a BT belongs to is detach-then-attach; the entry's `Id` and `CreatedAt` are preserved either way.
  - **Line-level (editable on an `Uncleared` line):** `AccountId`, `Amount`, `Description`. The line may also be removed, and new lines may be added — i.e. the *set* of `Uncleared` lines is freely reshapeable.
  - **Line-level (editable on a `Cleared` or `Reconciled` line):** `Description` only. `AccountId` and `Amount` are frozen and matched by `Id` against the existing entry. The line cannot be removed.
  - **Preserved across edits:** `Cleared` and `Reconciled` lines keep their `Id`, `CreatedAt`, and `ReconciliationStatus`. `Uncleared` lines that the client re-references by `Id` keep theirs; lines dropped from the edit body are deleted; new lines get server-assigned `Id` and default to `ReconciliationStatus.Uncleared`. Editing never mutates `ReconciliationStatus` on existing lines — flipping status is the job of the (currently unimplemented) reconciliation pass.
  - **Wire shape:** `PUT /api/journal-entries/{id}` with the desired final-state body. The server validates (a) every line with an `Id` whose current `ReconciliationStatus != Uncleared` appears unchanged in `AccountId` and `Amount`, (b) the line amounts sum to zero per `Currency`, (c) standard account / currency / counterparty checks.
  - **Cash entries** (no referencing **BankTransaction**, no auto-`Cleared` bank-side line) are therefore fully editable — including the set of lines — while every line remains `Uncleared`. This is the intentional consequence of the per-line gate, and is effectively delete-and-recreate while preserving the entry's `Id`, `CreatedAt`, and any reference to it.
- **BankTransactions** remain immutable in their bank-supplied fields (`BankAccountId`, `BookingDate`, `Money`, `Description`, `CounterpartyName`, `CounterpartyAccountNumber`, the promoted SEPA / FX columns — `ValueDate`, `Reference`, `MandateId`, `SepaCreditorId`, `ForeignAmount`, `ForeignCurrencyCode`, `ExchangeRate` — plus `ImporterKey`, `RawSource`, and `RowHash`) regardless of `JournalEntry` editability. Three surfaces are mutable post-import: (1) `JournalEntryId`, mutated by **Attach** / **Detach** (ADR 0013), never via the JE PUT; (2) user-applied **Dismissed** metadata (`DismissedAt`, `DismissedReason`), set and cleared through a dedicated dismiss/undismiss action — see ADR 0013 — never via PATCH and never as a side effect of the **Categorisation flow**; and (3) the **BankTransactionMetadata** set, which is rebuilt by the extractor named in `ImporterKey` from `RawSource` and may be replaced wholesale on re-extraction (e.g. parser improvement, one-shot backfill).

## Deletion policy (v1)

- **BankTransaction** is immutable in its bank-supplied fields and never deleted. **Dismissed** is the appropriate "make this row stop showing in the **Inbox**" action and is reversible — the row remains queryable in the full BankTransaction list view.
- **JournalEntry** is deletable (its **JournalLines** cascade; any referencing **BankTransactions** have their `JournalEntryId` set to null via `ON DELETE SET NULL` and return to the **Inbox**). Editing is preferred for corrections.
- **Account**, **Counterparty**, **BankAccount** are hard-deletable; FK constraints block the delete when they're still referenced. No archival / `IsArchived` flag in v1 — add later if UI clutter becomes a real problem.

## Open questions

- When the **reconciliation pass** lands and introduces a user-driven `Cleared` → `Uncleared` demotion, does demoting a line re-unlock its `AccountId` / `Amount` for editing? Today, with no demotion path implemented, the per-line editability gate is effectively "the counter-side is editable, the bank-side is frozen". The demotion-vs-editability interaction becomes load-bearing only when the reconciliation pass arrives.
