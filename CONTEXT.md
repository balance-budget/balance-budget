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
An immutable record of one imported bank-statement row, tied to the user's own **BankAccount** that the row belongs to. Carries the **BookingDate**, signed **Amount** (positive = money in, negative = money out, from the bank's perspective), and **Currency**. The other side of the row is denormalised onto the same record as a free-text `Description` and optional `CounterpartyName` / `CounterpartyAccountNumber` — bank-agnostic fields every statement row carries; bank-specific extras (e.g. ING's transaction code, SEPA mandate ID, foreign-currency block) live inside the raw row blob and are not promoted to columns. Also carries `RawSource` (the original statement-row text as exported by the bank) for audit and re-parsing, and `RowHash` (a content hash of the raw row) for idempotent re-imports. The name matches ISO 20022 vocabulary and is intentionally distinct from a **JournalEntry** — a **BankTransaction** is *what the bank told us*; a **JournalEntry** is *what we did about it*. A **JournalEntry** *may* reference a **BankTransaction** via `JournalEntry.BankTransactionId?`; cash entries leave it null.
_Avoid_: Transaction (overloaded with DB transactions and Plaid/Stripe types), import row, statement line.

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
- A **JournalEntry** *may* reference a **BankTransaction** (when imported) and *may* reference a **Counterparty** (when one is identified). Cash entries have neither **BankTransaction** nor (necessarily) a **BankAccount**-bearing side; they always have at least a **Counterparty** or a free-text description.
- A **BankTransaction** is immutable once stored; the **JournalEntry** derived from it is editable. Re-imports are deduplicated by hash.

## Example dialogue

> **Dev:** "I bought groceries at Albert Heijn — is Albert Heijn an **Account**?"
> **Domain expert:** "No. Albert Heijn is a **Counterparty**. The **Accounts** involved are 'Groceries Expense' (debited) and your bank account (credited)."

## Flagged ambiguities

- "account" is used in everyday speech to mean both a ledger **Account** and a bank account. Inside the domain, **Account** always means the ledger account (debit-normal or credit-normal, with an **AccountType**); the banking product is a **BankAccount** (carries IBAN / account number / bank metadata). An **Account** may be linked to a **BankAccount** when it represents a real bank product.
- "transaction" is overloaded in everyday speech (DB transactions, bank-statement rows, payment-API events). Inside the domain, the bookkeeping event is a **JournalEntry**; the immutable record of a bank-statement row is a **BankTransaction**. "Transaction" as a bare term is avoided.

## Editing policy (v1)

- **JournalEntries** are editable, but the editable surface is intentionally narrow to avoid silently rewriting books:
  - **Entry-level (editable):** `Date`, `Description`, `CounterpartyId`.
  - **Entry-level (not editable):** `BankTransactionId`. Once an entry references a **BankTransaction**, that link is part of the audit trail and is not changed via edit — correct misattributions by deleting and recreating the entry.
  - **Line-level (editable):** `JournalLine.Description` only.
  - **Line-level (not editable):** `Amount`, `AccountId`, and the *set* of lines (no additions, no removals, no reordering). Correct these by deleting the **JournalEntry** and recreating.
  - **Preserved across edits:** every **JournalLine**'s `Id`, `CreatedAt`, and `ReconciliationStatus` survive a **JournalEntry** edit — editing the entry's description never resets a line's `Cleared`/`Reconciled` state.
  - **Trajectory:** this scope may tighten further to fully append-only with reversing entries; today's surface is the smallest set of edits that supports common typo-fixing without rewriting bookkeeping state.
- **BankTransactions** remain immutable regardless of `JournalEntry` editability.

## Deletion policy (v1)

- **BankTransaction** is immutable — never deleted.
- **JournalEntry** is deletable (its **JournalLines** cascade). Editing is preferred for corrections.
- **Account**, **Counterparty**, **BankAccount** are hard-deletable; FK constraints block the delete when they're still referenced. No archival / `IsArchived` flag in v1 — add later if UI clutter becomes a real problem.

## Open questions

_(none — foundational design complete)_
