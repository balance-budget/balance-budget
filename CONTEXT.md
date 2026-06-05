# Personal Finance Bookkeeping

A personal-finance tool backed by a rigorous **double-entry ledger**. The domain is the bookkeeping core — accounts, journal entries, postings. Budgets and labels are deliberately deferred; imports and reporting (**Insights**) sit *on top* of this ledger.

## Language

**Account**:
A ledger account in the double-entry accounting sense (e.g. "Groceries Expense", "ABN AMRO Checking", "Visa Credit Card"). Every Account has exactly one **AccountType** and a required, globally-unique **Code**. Accounts form a tree via a nullable `ParentAccountId` self-reference (the **Chart of accounts**): an Account is either **Postable** — a leaf that **JournalLines** may reference — or a non-postable placeholder, a parent whose balance is the roll-up of its descendants. Every Account in a subtree shares the same **AccountType** and **Currency**.
_Avoid_: bucket, category, envelope, payee (those are different concepts).

**Postable** (and the verb **post**):
A property of an **Account** (`IsPostable`, an explicit boolean). A Postable Account is a *leaf* that **JournalLines** may reference directly — to **post** is to record a **JournalLine** against an Account. A non-postable Account is a *placeholder*: it carries no **JournalLines** of its own and exists only to roll up its descendants (see **Chart of accounts**). Invariants: (a) a non-postable Account is never referenced by a **JournalLine**; (b) an Account with children is never Postable; (c) an empty placeholder (no children yet) and an empty leaf (no lines yet) are both legal. `IsPostable` is set explicitly and changes only via an explicit conversion — flipping a leaf to placeholder requires it to have zero **JournalLines**, and flipping a placeholder to leaf requires it to have no children. Nesting a child under a Postable leaf is therefore rejected; the parent must be converted to a placeholder first.
_Avoid_: the noun **"posting"** as an alias for a **JournalEntry** or **JournalLine** (still avoided — see those terms). The verb **post** and the adjective **Postable** are canonical; only the noun is suppressed.

**Code** (account code):
The required, globally-**unique** human key on an **Account** — the chart-of-accounts "account number" (e.g. `5110` Groceries, `5100` Food, `3900` Opening Balances). Stored as a short string, not an integer: real codes are segmented and zero-padded. Hierarchical numbering — a child's code sharing its parent's prefix — is a *convention*, not enforced; only global uniqueness is. Distinct from **Name** (which carries no uniqueness constraint at all) and from **BankAccount**'s `AccountNumber`, the bank-side identifier (see Flagged ambiguities).
_Avoid_: "account number" (collides with **BankAccount.AccountNumber**), "id" (that is the surrogate **AccountId**).

**Chart of accounts**:
The tree of **Accounts** formed by the nullable `ParentAccountId` self-reference, to arbitrary depth. Leaves are **Postable**; interior nodes are non-postable placeholders whose balance is the recursive signed sum of their descendant leaves. Every Account in a subtree shares one **AccountType** and one **Currency** (the homogeneity rule; the currency half relaxes when multi-currency lands). Re-parenting is allowed and re-rolls balances automatically (they are derived), guarded against cycles — an Account may not become its own ancestor. A parent with children cannot be deleted (FK `RESTRICT`); its children must be re-parented or removed first.
_Avoid_: category tree, folder, group hierarchy (those imply the banned **category**/bucket framing — this is a ledger account tree).

**AccountType**:
The accounting classification of an **Account**, one of the five standard types: **Asset**, **Liability**, **Equity**, **Income**, **Expense**. Determines normal balance (debit-normal for Asset/Expense; credit-normal for Liability/Equity/Income) and how the account contributes to reports.

**Account icon**:
The optional, user-chosen glyph displayed for an **Account** wherever its avatar renders. When unset, the **Account** displays its **AccountType**'s default icon; once set, it is the user's deliberate choice and survives type and parent changes (never reset implicitly — only cleared explicitly back to the default). Purely presentational: it never affects accounting semantics. The avatar's *color* is not user-chosen — it always derives from the **AccountType**.
_Avoid_: avatar (the rendered tile, which combines icon + type color), logo, image (it is a named glyph from a curated set, not an uploaded picture).

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

**Sign convention**:
The signed meaning of a **JournalLine**'s `Amount`: positive = debit, negative = credit (Beancount / Ledger-CLI convention). Debit-normal **Accounts** (**Asset**, **Expense**) increase on positive amounts; credit-normal **Accounts** (**Liability**, **Equity**, **Income**) increase on negative. Balance and report projections re-sign to a chosen focal perspective so, e.g., **Income** and **Expense** both read as positive magnitudes; the raw storage convention never changes.
_Avoid_: "positive means money in", inventing a per-report sign rule (report sign is always derived from this one).

**Currency**:
An ISO-4217 (or ISO-4217-like, for crypto) currency identified by its **CurrencyCode**, carrying a **MinorUnitScale** that determines how many minor units make one major unit.
_Avoid_: "currency type", "denomination".

**MinorUnitScale**:
The exponent for converting a **Money** amount to/from its display value. EUR → 2 (100 minor units = €1.00); JPY → 0; BTC → 8; ETH → 18. The only place rounding happens is at the input boundary when parsing a major-unit value into minor units. Stored as a column on the **Currency** reference table.

**Currency** (entity):
A reference-data row in the `Currency` table: `(Code: string PK, Name: string, MinorUnitScale: int, Symbol?: string)`. Seeded on migration with common ISO 4217 currencies. **Accounts** and **BankAccounts** reference currencies by code (FK). New currencies (incl. crypto) are added by inserting a row, not by changing code.

**Reference data**:
Seed data that is part of the domain in *every* environment and that the app depends on to function — the **Currency** rows and the canonical `Opening Balances` **Equity** **Account**. Identical in development and production; real domain data, not illustrative and not disposable.
_Avoid_: sample data, fixtures, test data (those name the disposable development-only set — see **Development sample data**).

**Development sample data**:
Illustrative, disposable ledger content — **Accounts**, **Counterparties**, **BankAccounts**, **BankTransactions**, **JournalEntries** — that exists only to exercise the app during local development. Carries no domain meaning, never appears in production, and is kept current-dated so time-relative views (month-to-date, the current **Reporting period**) always have data. A **user** (the Development login) is provided alongside it so the shared ledger is reachable. Distinct from **Reference data**, which is real domain data present in every environment. (How it is produced is an implementation concern.)
_Avoid_: fixtures, demo data; bare "seed data" (ambiguous — **Reference data** is seeded too).

**Sign convention** (for **JournalLine.Amount**):
Positive = **debit**, negative = **credit**. The zero-sum invariant on a **JournalEntry** is therefore `SUM(Amount) = 0` per **Currency**. Per-**Account** running balance:
- **Asset** / **Expense** (debit-normal): balance = `SUM(Amount)`.
- **Liability** / **Equity** / **Income** (credit-normal): balance = `-SUM(Amount)`.

**Counterparty**:
The real-world party on the other side of a transaction (e.g. "Albert Heijn", "Employer X", a friend you split a bill with). Distinct from **Account** — counterparties are *not* ledger accounts; they are metadata on a **JournalEntry** that records "who".
_Avoid_: payee account, vendor account, expense account (those conflate counterparty with **Account**).

**BankAccount**:
A real-world bank or card account known to the system. Carries a **BankAccountType** (`Current`, `Savings`, or `Card`) and the bank-side identifier columns appropriate to that type — `Iban`, `AccountNumber`, and **CardIdentifier**. Also carries optional `Bic` / `BankName` / `AccountHolderName`, an optional `ImporterKey` naming the extractor to use for future statement imports onto this BankAccount, and a `CurrencyCode` that is required when the **BankAccount** is one of yours (`AccountId` set) and optional when it belongs to a **Counterparty** (`CounterpartyId` set). Schema-level CHECK constraints enforce the per-**BankAccountType** identifier requirements (`Current` → `Iban`; `Savings` → `Iban` or `AccountNumber`; `Card` → `CardIdentifier`), the conditional currency rule, and `Type='Card' ⇒ AccountId IS NOT NULL` (Card BankAccounts are owned-only — counterparty cards never appear on your statements). Owned by exactly one of: an **Account** (`BankAccount.AccountId` set — this is one of yours) or a **Counterparty** (`BankAccount.CounterpartyId` set — this belongs to a counterparty). The XOR is enforced as a single-table CHECK constraint. Used during imports to resolve "the IBAN, account number, or card identifier on the other side of this statement row" to either a self-transfer or a known **Counterparty**.
_Avoid_: bank account details, IBAN entry, payment instrument.

**BankAccountType**:
The shape classification of a **BankAccount**, one of `Current`, `Savings`, or `Card`. Drives which identifier column(s) are required on the row (`Current` → `Iban`; `Savings` → `Iban` or `AccountNumber`; `Card` → **CardIdentifier**) and which `ImporterKey`s are valid for the BankAccount (each extractor declares the **BankAccountType** it supports; the service layer refuses an `ImporterKey`/`Type` pair where the extractor's declared type disagrees with the BankAccount's type). `Card` is reserved for owned BankAccounts (`AccountId IS NOT NULL`); counterparty BankAccounts default to `Current`. Distinct from **AccountType** — which classifies a ledger **Account** by debit/credit normality — and conceptually orthogonal to it (e.g. a Card **BankAccount** typically belongs to a Liability **Account**; a Savings **BankAccount** to an Asset **Account**).
_Avoid_: "kind", "product type", or any phrasing that collides with **AccountType**.

**CardIdentifier**:
The masked card-number string identifying a `Card`-type **BankAccount** for matching against credit-card statement headers on import. Stored normalised (uppercase, spaces stripped) in the format the statement parser emits — e.g. ING's `"1234 **** **** 5678"` becomes `"1234************5678"`. Full PANs never reach the system; only the first-4 / last-4 the statement reveals. Required when **BankAccountType** is `Card`, and the unit of equality the credit-card extractor checks before allowing rows to import.
_Avoid_: "card number" (ambiguous with PAN), "PAN" (which is the full 16-digit number, never stored).

**Register**:
The per-**Account** view of bookkeeping activity — what a banking or accounting UI shows when you "open an account": a chronological list of every **JournalLine** posted to that **Account**, enriched with the **JournalEntry** header (date, description, **Counterparty**) and the offsetting side. *Derived*, not stored — a projection of **JournalLines** filtered to one **Account**. Distinct from a **JournalEntry** (which is the full multi-line bookkeeping event) and from a **BankTransaction** (which is the import-side record). One **JournalEntry** appears in two or more **Registers** — once per **Account** it touches. For a non-postable placeholder (see **Postable**), the Register is the **union of its descendant leaves' JournalLines**, merged newest-first; intra-subtree self-transfers appear as both legs (no elimination) and net to zero in the rolled-up balance.
_Avoid_: statement (sounds like a printed bank statement — that's a separate concept), ledger (the whole book, not one account), transaction (overloaded — see above), feed.

**RegisterRow**:
One row in a **Register** — derived from exactly one **JournalLine** on the focal **Account**. Carries the focal-account-signed **Money** amount (positive = money in to the focal account, negative = out — *not* the raw debit/credit sign), the **JournalEntry** header (`Date`, `Description`, `CounterpartyId`/`CounterpartyName`), the focal **JournalLine**'s `ReconciliationStatus` and `Description`, and the offsetting side as a list of `(AccountId, AccountName, Amount)` — one entry per non-focal **JournalLine** on the same **JournalEntry**. The list is single-element for a simple two-leg entry and multi-element for a split (e.g. one €100 purchase divided across `Groceries +60` and `Household +40`). UI renders the first element by name and the rest as a "+N" hint; the row's focal amount remains the sum of the focal line(s), so it always matches what a bank statement would show.

**Activity**:
The chronological, all-**Account** summary view of the ledger — one row per **JournalEntry**, newest first, each row showing the entry's net effect on net worth (signed per the **Sign convention**) and a `from → to` label. *Derived*, not stored — a projection over every **JournalEntry**. The everyday "what happened across all my accounts" feed; the home of the in-list `?q=` filter (matching **JournalEntry** `Description` *or* linked **Counterparty** `Name`). Distinct from the **Register** (scoped to one **Account**) and from the **Journal** (the formal debit/credit-per-line presentation — see below). Surfaced in the nav as "Activity" at `/activity`.
_Avoid_: ledger, transactions, feed, statement (each is reserved or avoided elsewhere), journal (reserved for the formal view).

**Journal** (reserved):
The formal *book of original entry* — the double-entry, one-line-per-debit-and-credit presentation of **JournalEntries**, the view an accountant recognises as "the journal". **Not yet built**: today the everyday all-account view is **Activity** (a collapsed one-row-per-entry summary), and a single entry's full line breakdown lives on its detail page (`/journal/$id`). The bare word "Journal" is reserved for this formal presentation when it lands; do not use it for the **Activity** feed.
_Avoid_: using "Journal" for the summary feed (that's **Activity**) or for the per-**Account** view (that's the **Register**).

**BankTransaction**:
An immutable record of one imported bank-statement row, tied to the user's own **BankAccount** that the row belongs to. Carries the **BookingDate**, signed **Amount** (positive = money in, negative = money out, from the bank's perspective), and **Currency**. The other side of the row is denormalised onto the same record as a free-text `Description` and optional `CounterpartyName` / `CounterpartyAccountNumber` — bank-agnostic fields every statement row carries. In addition, a fixed set of **bank-agnostic SEPA / ISO-20022 fields** is promoted to columns when present on the row: `ValueDate`, `Reference`, `MandateId`, `SepaCreditorId`, and an FX block (`ForeignAmount`, `ForeignCurrencyCode`, `ExchangeRate`). Anything else the extractor parses (ING's transaction code, SEPA creditor name/address, card sequence, FX markup/fee, etc.) lives in a typed **BankTransactionMetadata** key-value blob attached to the row (see [[BankTransactionMetadata]]). The row also carries `ImporterKey` (identifying which extractor produced it — null for manually-created rows), `RawSource` (the original statement-row text as exported by the bank) for audit and re-parsing, and `RowHash` (a content hash of the raw row) for idempotent re-imports. The name matches ISO 20022 vocabulary and is intentionally distinct from a **JournalEntry** — a **BankTransaction** is *what the bank told us*; a **JournalEntry** is *what we did about it*. A **BankTransaction** *may* reference a **JournalEntry** via `BankTransaction.JournalEntryId?` — set when the row is **Categorised** (a new **JournalEntry** is created) or **Attached** (the row is linked to an existing self-transfer **JournalEntry** that the other-side statement already produced); cash **JournalEntries** are not referenced by any **BankTransaction**. A **BankTransaction** may carry user-applied `DismissedAt` / `DismissedReason` metadata recording a **Dismissed** state — these fields, alongside `JournalEntryId` (mutated by **Attach** / **Detach**) and the **BankTransactionMetadata** set (which is rebuilt by re-extracting from `RawSource`), are the only mutable surface on the row; all other bank-supplied fields are immutable.
_Avoid_: Transaction (overloaded with DB transactions and Plaid/Stripe types), import row, statement line.

**BankTransactionMetadata**:
The set of typed, named extras an **IBankTransactionExtractor** parses out of a statement row that are *not* promoted to columns on **BankTransaction**. Modelled as a key-value side table: every entry is `(BankTransactionId, Key, StringValue | IntegerValue)` where exactly one value column is populated — string for free-text values (e.g. `IngTransactionCode = "IDX"`, `SepaCreditorName = "Vattenfall"`), integer for amounts in minor units or count-like values (e.g. `ForeignMarkUp.Amount = 150`, `CardSequence.Number = 3`). Keys are a global namespace owned across all extractors: bank-agnostic where the concept is shared (`SepaCreditorName`, `OtherParty`), bank-prefixed where genuinely bank-specific (`IngTransactionCode`, `IngMutatiesoort`). Nested values flatten with dotted keys (`ForeignMarkUp.Amount`, `ForeignMarkUp.CurrencyCode`). Distinct from the promoted SEPA / FX columns on **BankTransaction** (which are first-class, indexable, and present on the list view) and from `RawSource` (which is the immutable original-bytes audit trail). **BankTransactionMetadata** is *derived* from `RawSource` by the extractor named in `BankTransaction.ImporterKey` and can be rebuilt from `RawSource` at any time — it is the only field on a **BankTransaction** that an extractor may rewrite for a row that already exists, and the rebuild path is how parser improvements reach historical rows.
_Avoid_: import attributes, bank-row extras, custom fields, properties bag.

**Inbox**:
The derived set of **BankTransactions** that have no referencing **JournalEntry** and no recorded **Dismissed** state — the starting point of the **Categorisation flow**. *Derived*, not stored: the filter is `b.JournalEntryId IS NULL AND b.DismissedAt IS NULL`. Defaulted-sorted oldest-first so the user works the queue in statement order. Each Inbox row carries an optional **Attach hint** (`MatchingJournalEntryId`) when the **Attach predicate** uniquely identifies a self-transfer **JournalEntry** the row should link to — surfaces as a one-click `Attach` action alongside `Categorise` and `Dismiss`. Distinct from the full **BankTransaction** list view, which shows every imported row regardless of state.
_Avoid_: queue, unmatched list, pending imports.

**Dismissed**:
A terminal state of a **BankTransaction**, recorded as `DismissedAt` (UTC timestamp) plus `DismissedReason` (short free-text) on the row itself. Used when no **JournalEntry** should ever be created for the row and no existing **JournalEntry** is the right **Attach** target — e.g. a test transaction, a fee corrected elsewhere, a row the user explicitly chooses not to categorise. (The sibling of a self-transfer is handled via **Attach**, not Dismiss.) Reversible via undismiss — the row returns to the **Inbox**. User-applied metadata, *not* a mutation of bank-supplied fields; set and cleared only through a dedicated dismiss/undismiss action, never via PATCH or the **Categorisation flow**.
_Avoid_: archived, ignored, deleted (the row still exists and remains immutable in its bank-supplied fields).

**Categorisation flow**:
The user-driven process of producing exactly one **JournalEntry** for one **BankTransaction** — or, for the sibling of a self-transfer, **Attaching** the row to an existing **JournalEntry**. When the BT's `CounterpartyAccountNumber` resolves (via exact match on `BankAccount.Iban`) to one of your own **BankAccounts**, the flow recognises a **self-transfer in progress** and pre-fills the counter-side **Account** with that own-**Account** (leaving `CounterpartyId` null); otherwise the counter-side resolves through an exact-IBAN `Counterparty` match plus a last-used-**Account** heuristic. The new **JournalEntry** is created atomically with the bank-side **JournalLine** in `Cleared` **ReconciliationStatus** and counter-side line(s) in `Uncleared`. The flow also offers a **manual JE-picker** that lets the user attach to an existing **JournalEntry** when the Inbox's strict predicate did not auto-detect a match. Exposed as the composite endpoint `POST /api/bank-transactions/{id}/categorize` (create-new path) and `POST /api/bank-transactions/{id}/attach` (attach path). Multi-Account splits are modelled as multiple **JournalLines** within the single created **JournalEntry**, summing to `−BT.Amount` on the counter side.
_Avoid_: import flow (already used for parsing CSVs into BankTransactions), assignment, classification.

**Self-transfer**:
A **JournalEntry** that moves money between two of your own **Accounts** — e.g. Current (**Asset**) → Savings (**Asset**), or Current (**Asset**) → Credit Card (**Liability**) when paying down the card. Every **JournalLine** references an **Account** that is yours (`Account` linked via `BankAccount.AccountId`); there is no external party, so `CounterpartyId` is `null`. When both sides of the movement appear as imported **BankTransactions** (one per statement), the first BT to be categorised creates the **JournalEntry** with both lines on the user's own **Accounts** — its bank-side line is `Cleared`, the counter-side line stays `Uncleared` waiting for the sibling. When the sibling BT later imports, the **Inbox** surfaces a one-click **Attach** action that links it to the existing **JournalEntry** and flips the matching line to `Cleared`. A self-transfer **JournalEntry** is therefore referenced by one or two **BankTransactions** over its lifetime, and is the only `JournalEntry` shape allowed to be referenced by more than one.
_Avoid_: internal transaction, transfer (bare), between-accounts entry. "Transaction" is overloaded; "transfer" alone is ambiguous (some systems use it for any movement, including to a counterparty).

**Insights**:
The date-ranged, exploratory reporting area of the app, sitting *on top of* the ledger — distinct from the fixed at-a-glance **Dashboard** home. The user picks a **Reporting period** and a single **Currency**, then reads one or more **Reports**: in v1 the **Distribution** and the **Money flow**. Surfaced in the nav as "Insights" at `/reports`.
_Avoid_: analytics; "reporting" as the section noun (the section is **Insights**, an individual view is a **Report**); **Dashboard** (the existing summary home, not date-ranged).

**Report**:
One view within **Insights**, scoped to a **Reporting period** and a single **Currency**. v1 ships two — the **Distribution** and the **Money flow**.
_Avoid_: chart (the chart is the rendering; the **Report** is the concept), widget, tile.

**Reporting period**:
The `[from, to]` **inclusive** window of calendar dates that scopes a **Report**. Membership is always decided by the **JournalEntry** `Date` — never a **BankTransaction**'s **BookingDate** or **ValueDate** (those are import-side). Offered as presets (this / last month, this / last year, last 30 / 90 days) plus a custom range; defaults to the current month.
_Avoid_: timeframe, date filter, "as of" (that names a point-in-time **Balance**, not a window).

**Net movement**:
An **Account**'s signed net change over a **Reporting period** — the window-scoped analogue of **Balance** (which is the all-time running total). Computed with the same **Sign convention** as **Balance** (debit-normal **Asset** / **Expense** vs credit-normal **Liability** / **Equity** / **Income**), but summed only over **JournalLines** whose **JournalEntry** `Date` falls inside the period. For a temporary P&L **Account** (**Income** / **Expense**) the Net movement is its period total; for a balance-sheet **Account** (**Asset** / **Liability** / **Equity**) it is the change in its **Balance** across the period. The quantity the **Money flow** uses to place each **Account** on the in- or out-side.
_Avoid_: contribution (collides with "contribution margin"), delta, period balance, turnover.

**Distribution**:
A **Report** breaking down **Net movement** across one **AccountType** family — either **Income** ("where money came from") or **Expense** ("where money went") — over a **Reporting period**, rolled up the **Chart of accounts** tree and drillable one level at a time. Amounts are net: a refund credited to an **Expense** lowers that slice; a clawback lowers the **Income** slice. A subtree whose **Net movement** is net-negative in the period is excluded from the part-of-whole rendering and surfaced as a note rather than drawn as a slice.
_Avoid_: category breakdown, spending by category (category is banned — the slices are **Income** / **Expense** **Accounts**).

**Money flow**:
A **Report** depicting the whole ledger's in/out picture over a **Reporting period** as a single-hub flow diagram. Every **Account** contributes exactly one flow sized by its **Net movement**, and its side is chosen by sign: money *in* on the source side (**Income**, plus balance-sheet **Accounts** that shrank — a drained savings **Account**, new borrowing on a card), money *out* on the exit side (**Expense**, plus balance-sheet **Accounts** that grew — savings, investments, debt paid down, cash left as a buffer). Sources and exits balance exactly by the double-entry identity `Σ Income − Σ Expense = Σ (balance-sheet Net movement)`. The **Income** / **Expense** sides render at full **Chart of accounts** depth (subtrees become intermediate nodes); balance-sheet **Accounts** render at top level (v1). A net-negative **Account** flips to the opposite side for that period.
_Avoid_: cash flow (a specific, loaded accounting statement), Sankey (the chart type, not the concept), in/out report.

## Relationships

- A **JournalEntry** owns two or more **JournalLines** whose amounts net to zero.
- Each **JournalLine** references exactly one **Account**.
- A **JournalEntry** *may* reference one **Counterparty**; **JournalLines** do not reference **Counterparties**.
- A **Counterparty** is never an **Account** (this is an explicit departure from Firefly III, which models each payee as an expense/revenue account).
- Each **Account** has exactly one **AccountType**.
- **Accounts** form a tree via a nullable `Account.ParentAccountId` self-reference, to arbitrary depth; cycles are rejected (an **Account** may not become its own ancestor).
- Every **Account** in a subtree shares one **AccountType** and one **CurrencyCode** (the homogeneity rule; the currency half relaxes when multi-currency lands).
- An **Account** is either **Postable** (a leaf that **JournalLines** may reference) or a non-postable placeholder; an **Account** with children is never **Postable**, and a **JournalLine** never references a non-postable **Account**.
- `Account.Code` is required and globally **unique**; `Account.Name` carries no uniqueness constraint.
- A **BankAccount** may link only to a **Postable** **Account** (in addition to the existing `UNIQUE(AccountId)`).
- Every **JournalLine** carries a **Money** amount; its **Currency** is inherited from its **Account**.
- A **BankAccount** belongs to *exactly one* of: one **Account** (via `BankAccount.AccountId`) or one **Counterparty** (via `BankAccount.CounterpartyId`) — never both, never neither, enforced by CHECK constraint.
- A **BankAccount** that belongs to an **Account** must have a `CurrencyCode`; one that belongs to a **Counterparty** may leave `CurrencyCode` null. Enforced by CHECK constraint and by service-layer validation.
- An **Account** has at most one **BankAccount** (enforced by `UNIQUE(AccountId)` on **BankAccount** where non-null).
- A **BankAccount** has exactly one **BankAccountType** (default `Current`). A `Card` **BankAccount** must be owned by an **Account** (`AccountId IS NOT NULL`). Identifier-column requirements vary by **BankAccountType** and are enforced by CHECK constraint.
- A **BankAccount**'s `ImporterKey` — when set — must reference an extractor whose declared `SupportedType` equals the BankAccount's **BankAccountType**. Enforced in the service layer at write time and at import dispatch.
- A **JournalEntry** *may* be referenced by one or more **BankTransactions** (when imported) and *may* reference a **Counterparty** (when one is identified). Cash entries have no referencing **BankTransaction** and no (necessarily) **BankAccount**-bearing side; they always have at least a **Counterparty** or a free-text description.
- A **BankTransaction** references at most one **JournalEntry** via the nullable scalar FK `BankTransaction.JournalEntryId`. A **JournalEntry** is referenced by zero, one, or many **BankTransactions**; multiple references are reserved for self-transfers (every line on an own-**Account**, `CounterpartyId IS NULL`) and enforced in the service layer as part of the **Attach predicate**. Splits are modelled as multiple **JournalLines** within one **JournalEntry**, not as multiple **JournalEntries** sharing a **BankTransaction**.
- A **BankTransaction** is immutable in its bank-supplied fields once stored; the **JournalEntry** referenced by it is editable. The mutable surface on a **BankTransaction** is `JournalEntryId` (via **Attach** / **Detach**), the **Dismissed** metadata (`DismissedAt`, `DismissedReason`), and the **BankTransactionMetadata** set (rebuilt by re-extraction from `RawSource`). Re-imports are deduplicated by hash.

## Flagged ambiguities

- "account" is used in everyday speech to mean both a ledger **Account** and a bank account. Inside the domain, **Account** always means the ledger account (debit-normal or credit-normal, with an **AccountType**); the banking product is a **BankAccount** (carries IBAN / account number / bank metadata). An **Account** may be linked to a **BankAccount** when it represents a real bank product. Relatedly, the **Account**'s human key is its **Code** (a chart-of-accounts number); the bank-side identifier on a **BankAccount** is its `AccountNumber`. These are different things — never call the ledger Account's **Code** an "account number".
- "transaction" is overloaded in everyday speech (DB transactions, bank-statement rows, payment-API events). Inside the domain, the bookkeeping event is a **JournalEntry**; the immutable record of a bank-statement row is a **BankTransaction**. "Transaction" as a bare term is avoided.
- "user" is an *access-control* concept, not a domain one. A user is a human login (`AspNetUsers` row) that gates entry to the app; multiple users share one ledger, and the ledger has no per-user data. A user is *not* a **Counterparty** (the real-world party on the other side of a **JournalEntry**), is *not* an **Account** (a ledger account), and is *not* the **AccountHolderName** on a **BankAccount** (which is statement-row metadata about whoever owns a bank product). When the codebase says "user" it always means the logged-in human; when it means a counterparty or account-holder, it says so explicitly.

The **JournalEntry** editing and deletion rules (the per-`JournalLine` `ReconciliationStatus` gate, what is mutable on a **BankTransaction**, and cascade/RESTRICT behaviour) live in [ADR-0014](docs/adr/0014-journal-entry-editability.md).
