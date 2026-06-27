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

**Liquidity**:
A per-**Account** property, **Liquid** (the default) or **Illiquid**, meaningful only on **Asset** and **Liability** **Accounts**. A *user judgment* answering "do I budget with this money?" — not a market-liquidity fact: an investment account sellable in days may still be Illiquid because it is not day-to-day money. Illiquid **Accounts** (e.g. Mortgage, the house's value, pensions, locked deposits) are excluded from **Liquid net worth** but always count toward **Net worth**. Unlike **AccountType** and **Currency**, Liquidity is *not* subject to the homogeneity rule — children in one subtree may differ (an emergency fund and a locked five-year deposit can share one "Savings" placeholder).
_Avoid_: short-term / long-term **as a Liquidity label** — that time dimension is its own orthogonal axis (see **Horizon**); Liquidity answers "can I touch it?", not "when will I draw on it?" (a 12-month term deposit is Medium-term yet Illiquid). Also avoid current / non-current (the accounting balance-sheet split; close, but Liquidity is a budgeting judgment, not a maturity test).

**Horizon**:
A per-**Account** property — **Short-term**, **Medium-term**, or **Long-term** — meaningful only on **Asset** and **Liability** **Accounts**, *orthogonal* to **Liquidity**. It answers "*when* do I expect to draw on this money?", a budgeting judgment (not a contractual maturity date): **Short-term** is day-to-day spending money (relevant today; e.g. a Current account), **Medium-term** is reserves you'll likely touch this year (e.g. a Savings account, typically an order of magnitude larger), **Long-term** is wealth held for the decade (e.g. real estate offsetting a mortgage, pensions, locked deposits — typically two orders of magnitude larger). Horizon exists to keep these wildly different magnitudes on separate trend charts so a large Savings balance doesn't flatten the Current-account trend into noise. Independent of **Liquidity**: a locked five-year deposit is **Medium-term** *and* **Illiquid**; the house is **Long-term** *and* **Illiquid**; a Current account is **Short-term** *and* **Liquid**. Like Liquidity, Horizon is *not* subject to the homogeneity rule (children in a subtree may differ) and never affects accounting semantics — it is a presentation/grouping axis only. Defaulted on account creation (Illiquid → Long-term; Liquid + linked **Savings** **BankAccount** → Medium-term; otherwise Short-term) and then freely editable.
_Avoid_: maturity / term (implies a contractual end date — a Current account and a house have none), current / non-current and short-term / long-term as **Liquidity** synonyms (different axis — see above), tier / band / bucket (the last collides with the banned **category**/envelope framing).

**Loan**:
A borrowing agreement with a lender — a mortgage, a personal loan, a car loan — whose outstanding debt the ledger tracks. Not mortgage-specific: a mortgage is one kind of Loan. A Loan consists of one or more **Loan Parts** and is represented in the **Chart of accounts** as one non-postable **Liability** **Account** (the loan) with one postable child Account per **Loan Part** — *always*, even when the Loan has a single part. The loan-level outstanding debt is the parent's roll-up balance.
_Avoid_: mortgage as the general term (one kind of Loan), liability (the **AccountType**, not the agreement), debt (vague).

**Loan Part**:
One component (Dutch: *leningdeel*) of a **Loan**, carrying its own interest rate, repayment type, start date, and term. Dutch mortgages routinely consist of several parts with different rates and types, and parts are added or split over a loan's life (rate renewals, renovations). Each Loan Part is represented by exactly one postable **Liability** **Account** under its **Loan**'s parent Account; the part's outstanding principal is that Account's balance.
_Avoid_: component (collides with UI components in this codebase), tranche, leningdeel in code (English canonical term is Loan Part).

**Loan-managed**:
A property of a postable **Account** that belongs to a **Loan Part** (and of the **Loan**'s parent Account). Generic flows — manual **JournalEntry** creation, **Reassign**, plain **Categorization** — refuse to target a Loan-managed Account; only loan-aware flows post to it. Within a loan-aware flow the engine's proposed amounts are *defaults the user may edit*: the bank's actual charge always wins over the calculation, and the schedule reconciles against posted reality, never the other way around.
_Avoid_: locked (too absolute — loan-aware flows post freely), read-only, system account.

**Loan payment**:
The regular (monthly) composite debit a lender collects, covering interest for every **Loan Part** plus principal for the amortizing ones. Recorded as one **JournalEntry**: one bank-side line, one principal line per amortizing **Loan Part** (on that part's Account), and one interest line per **Loan Part** (all on the **Loan**'s single interest **Expense** Account), each counter-line attributed to its **Loan Part**. Produced via the loan-aware **Categorization flow**, pre-filled from the engine's proposal.
_Avoid_: installment, termijnbedrag (Dutch; UI/code use Loan payment), mortgage payment (mortgage-specific).

**Stub period**:
The short first stretch of a **Loan Part**'s life when its start date falls mid-month, so the first charge covers only a fraction of a month. For that fraction the lender bills *interest only* — the month's interest prorated by days elapsed (e.g. 19/30) with no principal — even for amortizing parts. The lender typically bundles this stub charge together with the first full month into a single oversized first **Loan payment**. The engine does not model stub proration: the first **Loan payment** is recorded as a manual override in the loan-aware **Categorization flow**, folding each part's stub interest into that part's full-month interest line (so **Loan Part** attribution is preserved) while principal stays at the full month. Only the principal must be exact — it sets the anchor balance the **Projection** runs from; the inflated first-month interest affects expense reporting and the actuals curve only, never the **Projection**.
_Avoid_: partial period (vague), pro-rata month, first-month payment (describes proration, not a separate payment type).

**Extra repayment**:
A principal-only payment outside the regular **Loan payment**, applied to one chosen **Loan Part** (Dutch: *extra aflossing*). Categorized through the loan-aware flow; any prepayment penalty (*boeterente*) is an additional interest-expense line on the same **JournalEntry**. Follows the Dutch-default policy: the part's end date is unchanged and the recalculated (lower) payment emerges automatically from the engine, which always derives the payment from current balance, rate, and remaining term. Term-shortening exists only as a what-if lever in simulation.
_Avoid_: prepayment (ambiguous with paying early in the month), overpayment.

**Rate period**:
One entry in a **Loan Part**'s effective-dated list of interest rates: an effective date, an annual nominal rate, and an optional fixed-until date (Dutch: *rentevaste periode*). The rate in force for a month is the latest entry effective on or before it; future-dated entries (an accepted renewal offer) are legitimate and feed the projection. Monthly interest is the annual rate ÷ 12 on the balance at period start. Rate periods are dated *facts*, not replayed events — and because the **Projection** is computed, never materialized, a Rate period is freely editable and deletable to correct a mistake (wrong rate, wrong date, wrong fixed-until); the only guards are that a **Loan Part** always keeps **≥ 1** Rate period and that `(LoanPartId, EffectiveDate)` stays unique. Correcting a fact is not the same as rewriting history: editing a Rate period never touches posted **Loan payment** **JournalLines** (the bank's actual charge), only future proposals and the projected curve.
_Avoid_: interest change event (it is a fact with an effective date, not a replayed event), APR (effective-rate concept; this is the nominal rate).

**Projection**:
The derived future of the ledger, in two flavors that share one model: a **Loan**'s amortization, and a **Liquid** balance's forward curve (driven by **JournalEntryTemplate**s plus **Typical spend**, surfaced in **Outlook**). *Computed, never stored*: a pure function of definitions (a Loan's part definitions and **Rate periods**, or the liquid account's templates and trailing actuals) and current ledger balances, projected forward from an anchor of *(balance now, …, horizon)* — never replayed from inception. Past periods come from ledger actuals, future periods from the Projection; a what-if **Scenario** is the same computation with hypothetical overrides (extra repayments, rate changes, a cancelled or added template) overlaid, ephemeral and unpersisted. For the loan flavor, "per period" is per **Loan Part** (expected interest, principal, payment, remaining balance); for the liquid flavor it is per month (expected inflow, outflow, end-of-month balance, with **Typical spend** as a band).
_Avoid_: amortization schedule as a noun implying a stored table (nothing is materialized), forecast (vague).

**Construction deposit**:
An **Asset** **Account** holding mortgage money the lender has *not yet disbursed* — funds earmarked for building or renovation that you draw down as invoices come in (Dutch: *bouwdepot*). A plain, **not Loan-managed** Account: generic flows post to it freely — draw-downs are ordinary **JournalEntries** (direct disbursement: the lender pays the contractor, no **BankTransaction**) or **Self-transfers** (reimbursement: the lender repays you into Checking). A **Loan** *may* reference one Construction deposit, alongside a deposit-interest **Income** **Account** and a single editable annual rate, used only to compute the **Deposit-interest offset** on its **Loan payment**. Typically **Illiquid** (earmarked, not day-to-day money).
_Avoid_: bouwdepot (Dutch), escrow (third-party connotation), construction loan (that is the borrowing, not the deposit), building fund.

**Deposit-interest offset**:
The **Income** line the loan-aware **Loan payment** proposal adds during construction — `Construction deposit balance at period start × monthly deposit rate`, credited to the **Loan**'s configured deposit-interest **Income** **Account**. It offsets the gross interest **Expense** so the **JournalEntry**'s net equals the *single netted debit* the lender actually collects; the deposit interest is *consumed as compensation* and never lands in the **Construction deposit** balance (which only shrinks via draws). Proposed as an editable default like every other amount, capped so it never exceeds the entry's gross interest. The lender credits it one period in arrears — computed on the *previous* period's deposit balance — so the engine's pre-fill (which uses the *current* balance) runs slightly low during draw-down and is corrected to the bank's figure each month. The pre-fill drops to €0 once the deposit balance reaches €0 (construction complete), but the offset *outlives* the deposit by one period: a final credit arrives the month after the balance empties, which the proposal no longer pre-fills and must be added by hand. Carries no **Loan Part** attribution — it is a loan-level line.
_Avoid_: netting (describes the mechanism, not the line), deposit interest income (true, but the canonical concept is its offset role).

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
The masked card-number string identifying a `Card`-type **BankAccount** for matching against credit-card statement headers on import. Stored normalized (uppercase, spaces stripped) in the format the statement parser emits — e.g. ING's `"1234 **** **** 5678"` becomes `"1234************5678"`. Full PANs never reach the system; only the first-4 / last-4 the statement reveals. Required when **BankAccountType** is `Card`, and the unit of equality the credit-card extractor checks before allowing rows to import.
_Avoid_: "card number" (ambiguous with PAN), "PAN" (which is the full 16-digit number, never stored).

**Register**:
The per-**Account** view of bookkeeping activity — what a banking or accounting UI shows when you "open an account": a chronological list of every **JournalLine** posted to that **Account**, enriched with the **JournalEntry** header (date, description, **Counterparty**) and the offsetting side. *Derived*, not stored — a projection of **JournalLines** filtered to one **Account**. Distinct from a **JournalEntry** (which is the full multi-line bookkeeping event) and from a **BankTransaction** (which is the import-side record). One **JournalEntry** appears in two or more **Registers** — once per **Account** it touches. For a non-postable placeholder (see **Postable**), the Register is the **union of its descendant leaves' JournalLines**, merged newest-first; intra-subtree self-transfers appear as both legs (no elimination) and net to zero in the rolled-up balance.
_Avoid_: statement (sounds like a printed bank statement — that's a separate concept), ledger (the whole book, not one account), transaction (overloaded — see above), feed.

**RegisterRow**:
One row in a **Register** — derived from exactly one **JournalLine** on the focal **Account**. Carries the focal-account-signed **Money** amount (positive = money in to the focal account, negative = out — *not* the raw debit/credit sign), the **JournalEntry** header (`Date`, `Description`, `CounterpartyId`/`CounterpartyName`), the focal **JournalLine**'s `ReconciliationStatus` and `Description`, and the offsetting side as a list of `(AccountId, AccountName, Amount)` — one entry per non-focal **JournalLine** on the same **JournalEntry**. The list is single-element for a simple two-leg entry and multi-element for a split (e.g. one €100 purchase divided across `Groceries +60` and `Household +40`). UI renders the first element by name and the rest as a "+N" hint; the row's focal amount remains the sum of the focal line(s), so it always matches what a bank statement would show.

**Register summary**:
A time-bucketed aggregation of one **Register**: for each bucket (day, week, or month) in a date range, the net signed sum of the focal **Account**'s **JournalLine** amounts, normalized to the account's normal balance per the **Sign convention**, segmented by the account's direct children (deeper descendants roll up into their direct-child ancestor; a **postable** leaf yields a single segment, itself). *Derived*, not stored — like the **Register** it summarizes. Distinct from **Activity** (all-account, per-entry) and from a balance-over-time series (a Register summary shows per-period flow, not running balance).
_Avoid_: "activity" (reserved for the all-account feed), "history" / "trend" (suggest balance-over-time), "chart" (presentation, not the concept).

**Register preview**:
The newest-N **RegisterRows** of one **Account**'s **Register** — the short per-account excerpt the **Dashboard** shows beside each account card. *Derived*, not stored, like the **Register** it previews: same focal-account sign (positive = money in, negative = out, per **RegisterRow**) and same newest-first ordering, but truncated to a handful of rows and carrying only the fields a compact card renders (no offsetting-side list, no paging, no count). The **Dashboard** fetches the previews for *all* **Postable** **Accounts** in one batched request — **Accounts** with no activity are omitted — rather than one **Register** request per account. Distinct from the full **Register** (the complete, paged per-account view) and from **Activity** (the all-account feed).
_Avoid_: "recent activity" as a code/API term (collides with **Activity**, the reserved all-account feed — fine only as colloquial UI copy), "recent transactions" ("transaction" is overloaded — see **BankTransaction**), mini-statement.

**Posted account**:
The **Account** a **JournalLine** references — where the line was *posted* (see **Postable**). On a **RegisterRow** this is the account the focal line actually landed on: when the **Register** being viewed belongs to a non-postable placeholder, the posted account is one of its descendant leaves; when viewing a leaf, it is the viewed **Account** itself. Distinct from the **counter-account(s)** — the accounts on the *other* legs of the same **JournalEntry** — and from the **Counterparty** (a real-world party, not an **Account** at all).
_Avoid_: "source" / "source account" (ambiguous with bank-import origin), "owning account", "line account".

**Reassign**:
Re-pointing one **JournalLine** to a different **posted account** — the line's amount, description, **ReconciliationStatus**, and the rest of the **JournalEntry** (date, header, other legs) are untouched, so the entry still nets to zero. The target **Account** must be **postable** and share the line's **Currency**; **AccountType** may differ (reclassifying an **Expense** leg onto an **Asset** is legitimate). A frozen line (**ReconciliationStatus** `Cleared` or `Reconciled`) is never reassigned — which by construction also protects the bank-side leg of any **JournalEntry** referenced by a **BankTransaction**, since the **Categorization flow** and **Attach** always leave that leg `Cleared`. Bulk reassign is the same operation applied to an explicit set of lines, atomically: all lines move or none do.
_Avoid_: move (vague), recategorize (collides with the **Categorization flow**, which is about **BankTransactions**), transfer (reserved for **Self-transfer**).

**Activity**:
The chronological, all-**Account** summary view of the ledger — one row per **JournalEntry**, newest first, each row showing the entry's net effect on net worth (signed per the **Sign convention**) and a `from → to` label. *Derived*, not stored — a projection over every **JournalEntry**. The everyday "what happened across all my accounts" feed; the home of the in-list `?q=` filter (matching **JournalEntry** `Description` *or* linked **Counterparty** `Name`). Distinct from the **Register** (scoped to one **Account**) and from the **Journal** (the formal debit/credit-per-line presentation — see below). Surfaced in the nav as "Activity" at `/activity`.
_Avoid_: ledger, transactions, feed, statement (each is reserved or avoided elsewhere), journal (reserved for the formal view).

**Journal** (reserved):
The formal *book of original entry* — the double-entry, one-line-per-debit-and-credit presentation of **JournalEntries**, the view an accountant recognizes as "the journal". **Not yet built**: today the everyday all-account view is **Activity** (a collapsed one-row-per-entry summary), and a single entry's full line breakdown lives on its detail page (`/journal/$id`). The bare word "Journal" is reserved for this formal presentation when it lands; do not use it for the **Activity** feed.
_Avoid_: using "Journal" for the summary feed (that's **Activity**) or for the per-**Account** view (that's the **Register**).

**BankTransaction**:
An immutable record of one imported bank-statement row, tied to the user's own **BankAccount** that the row belongs to. Carries the **BookingDate**, signed **Amount** (positive = money in, negative = money out, from the bank's perspective), and **Currency**. The other side of the row is denormalized onto the same record as a free-text `Description` and optional `CounterpartyName` / `CounterpartyAccountNumber` — bank-agnostic fields every statement row carries. In addition, a fixed set of **bank-agnostic SEPA / ISO-20022 fields** is promoted to columns when present on the row: `ValueDate`, `Reference`, `MandateId`, `SepaCreditorId`, and an FX block (`ForeignAmount`, `ForeignCurrencyCode`, `ExchangeRate`). Anything else the extractor parses (ING's transaction code, SEPA creditor name/address, card sequence, FX markup/fee, etc.) lives in a typed **BankTransactionMetadata** key-value blob attached to the row (see [[BankTransactionMetadata]]). The row also carries `ImporterKey` (identifying which extractor produced it — null for manually-created rows), `RawSource` (the original statement-row text as exported by the bank) for audit and re-parsing, and `RowHash` (a content hash of the raw row) for idempotent re-imports. The name matches ISO 20022 vocabulary and is intentionally distinct from a **JournalEntry** — a **BankTransaction** is *what the bank told us*; a **JournalEntry** is *what we did about it*. A **BankTransaction** *may* reference a **JournalEntry** via `BankTransaction.JournalEntryId?` — set when the row is **Categorized** (a new **JournalEntry** is created) or **Attached** (the row is linked to an existing self-transfer **JournalEntry** that the other-side statement already produced); cash **JournalEntries** are not referenced by any **BankTransaction**. A **BankTransaction** may carry user-applied `DismissedAt` / `DismissedReason` metadata recording a **Dismissed** state — these fields, alongside `JournalEntryId` (mutated by **Attach** / **Detach**) and the **BankTransactionMetadata** set (which is rebuilt by re-extracting from `RawSource`), are the only mutable surface on the row; all other bank-supplied fields are immutable.
_Avoid_: Transaction (overloaded with DB transactions and Plaid/Stripe types), import row, statement line.

**BankTransactionMetadata**:
The set of typed, named extras an **IBankTransactionExtractor** parses out of a statement row that are *not* promoted to columns on **BankTransaction**. Modeled as a key-value side table: every entry is `(BankTransactionId, Key, StringValue | IntegerValue)` where exactly one value column is populated — string for free-text values (e.g. `IngTransactionCode = "IDX"`, `SepaCreditorName = "Vattenfall"`), integer for amounts in minor units or count-like values (e.g. `ForeignMarkUp.Amount = 150`, `CardSequence.Number = 3`). Keys are a global namespace owned across all extractors: bank-agnostic where the concept is shared (`SepaCreditorName`, `OtherParty`), bank-prefixed where genuinely bank-specific (`IngTransactionCode`, `IngMutatiesoort`). Nested values flatten with dotted keys (`ForeignMarkUp.Amount`, `ForeignMarkUp.CurrencyCode`). Distinct from the promoted SEPA / FX columns on **BankTransaction** (which are first-class, indexable, and present on the list view) and from `RawSource` (which is the immutable original-bytes audit trail). **BankTransactionMetadata** is *derived* from `RawSource` by the extractor named in `BankTransaction.ImporterKey` and can be rebuilt from `RawSource` at any time — it is the only field on a **BankTransaction** that an extractor may rewrite for a row that already exists, and the rebuild path is how parser improvements reach historical rows.
_Avoid_: import attributes, bank-row extras, custom fields, properties bag.

**Importer**:
The logical identity of a statement source — a (bank, **BankAccountType**) pairing such as "ING Current account" or "ING Credit card" — named by a stable `ImporterKey` (e.g. `Ing.CurrentAccount`, `Ing.CreditCard`). The key a **BankAccount** binds to and that is stamped on every **BankTransaction** an import produces. Deliberately *version-free*: a single real-world account spans statement-format eras over its life, so the era is never welded to the account — see **Statement layout**. The frontend renders an Importer as `BankName` (a proper noun the extractor declares, e.g. "ING") plus the **BankAccountType** word, translated; there is no human-readable label stored in backend code.
_Avoid_: extractor (the *code* implementing an Importer — `IBankTransactionExtractor`), parser, format, bank connector. Also avoid versioned keys like `Ing.CreditCard.V2` as a *stored* `ImporterKey` (the version belongs to the **Statement layout**, never the Importer).

**Statement layout**:
The concrete file format a single statement was exported in — one **Importer** may read several over time (e.g. ING's pre-2016 credit-card PDF versus the current one). The layout is *resolved per file by content sniffing* — never by filename or date — and a file whose structure matches no known layout fails the import loudly rather than being guessed at. An internal parsing detail of an **Importer**: it never appears in a stored `ImporterKey` and re-extraction re-sniffs it from `RawSource`.
_Avoid_: importer version, format version, parser version (the `.Vn` suffix is gone from stored keys).

**Statement detection**:
The drop-and-detect import flow: the user drops one or more statement files with no account chosen, and each file is matched to its target **BankAccount** by its **Account anchor**. Detection is *advisory* — it proposes a target; the per-**Importer** extract then re-asserts the file's content anchor against that account, so a mis-detected or mislabeled file fails loudly and can never write rows to the wrong account. Only an unambiguous single-account match imports automatically; no match, an ambiguous match, a non-importable target, or an unrecognized file is surfaced for the user to resolve manually rather than silently imported or dropped.
_Avoid_: auto-import (it is detection plus a guarded import, not an unconditional one), magic import, smart import.

**Account anchor**:
The bank-side identifier a statement file reveals about the account it belongs to — the IBAN / `AccountNumber` for current and savings exports, the **CardIdentifier** for card exports — used by **Statement detection** to resolve the target **BankAccount**. Taken from the fastest reliable source available (ING current/savings exports carry it in the filename; the credit-card PDF only in its content), but always re-validated against the file's content at extract time, so the filename is an accelerator and never an authority.
_Avoid_: account key, statement owner, IBAN (only one of several anchor forms).

**Inbox**:
The derived set of **BankTransactions** that have no referencing **JournalEntry** and no recorded **Dismissed** state — the starting point of the **Categorization flow**. *Derived*, not stored: the filter is `b.JournalEntryId IS NULL AND b.DismissedAt IS NULL`. Defaulted-sorted oldest-first so the user works the queue in statement order. Each Inbox row carries an optional **Attach hint** (`MatchingJournalEntryId`) when the **Attach predicate** uniquely identifies a self-transfer **JournalEntry** the row should link to — surfaces as a one-click `Attach` action alongside `Categorize` and `Dismiss`. Distinct from the full **BankTransaction** list view, which shows every imported row regardless of state.
_Avoid_: queue, unmatched list, pending imports.

**Dismissed**:
A terminal state of a **BankTransaction**, recorded as `DismissedAt` (UTC timestamp) plus `DismissedReason` (short free-text) on the row itself. Used when no **JournalEntry** should ever be created for the row and no existing **JournalEntry** is the right **Attach** target — e.g. a test transaction, a fee corrected elsewhere, a row the user explicitly chooses not to categorize. (The sibling of a self-transfer is handled via **Attach**, not Dismiss.) Reversible via undismiss — the row returns to the **Inbox**. User-applied metadata, *not* a mutation of bank-supplied fields; set and cleared only through a dedicated dismiss/undismiss action, never via PATCH or the **Categorization flow**.
_Avoid_: archived, ignored, deleted (the row still exists and remains immutable in its bank-supplied fields).

**Categorization flow**:
The user-driven process of producing exactly one **JournalEntry** for one **BankTransaction** — or, for the sibling of a self-transfer, **Attaching** the row to an existing **JournalEntry**. When the BT's `CounterpartyAccountNumber` resolves (via exact match on `BankAccount.Iban`) to one of your own **BankAccounts**, the flow recognizes a **self-transfer in progress** and pre-fills the counter-side **Account** with that own-**Account** (leaving `CounterpartyId` null); otherwise the counter-side resolves through an exact-IBAN `Counterparty` match plus a last-used-**Account** heuristic. The new **JournalEntry** is created atomically with the bank-side **JournalLine** in `Cleared` **ReconciliationStatus** and counter-side line(s) in `Uncleared`. The flow also offers a **manual JE-picker** that lets the user attach to an existing **JournalEntry** when the Inbox's strict predicate did not auto-detect a match. Exposed as the composite endpoint `POST /api/bank-transactions/{id}/categorize` (create-new path) and `POST /api/bank-transactions/{id}/attach` (attach path). Multi-Account splits are modeled as multiple **JournalLines** within the single created **JournalEntry**, summing to `−BT.Amount` on the counter side.
_Avoid_: import flow (already used for parsing CSVs into BankTransactions), assignment, classification.

**Self-transfer**:
A **JournalEntry** that moves money between two of your own **Accounts** — e.g. Current (**Asset**) → Savings (**Asset**), or Current (**Asset**) → Credit Card (**Liability**) when paying down the card. Every **JournalLine** references an **Account** that is yours (`Account` linked via `BankAccount.AccountId`); there is no external party, so `CounterpartyId` is `null`. When both sides of the movement appear as imported **BankTransactions** (one per statement), the first BT to be categorized creates the **JournalEntry** with both lines on the user's own **Accounts** — its bank-side line is `Cleared`, the counter-side line stays `Uncleared` waiting for the sibling. When the sibling BT later imports, the **Inbox** surfaces a one-click **Attach** action that links it to the existing **JournalEntry** and flips the matching line to `Cleared`. A self-transfer **JournalEntry** is therefore referenced by one or two **BankTransactions** over its lifetime, and is the only `JournalEntry` shape allowed to be referenced by more than one.
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

**Net worth**:
The all-time signed total `Σ Asset Balances − Σ Liability Balances` over *all* **Asset** and **Liability** **Accounts** in one **Currency**, regardless of **Liquidity**. The complete financial picture — the house and the mortgage both count.
_Avoid_: using "net worth" bare for the day-to-day budgeting headline (that is **Liquid net worth**); wealth, total balance.

**Liquid net worth**:
**Net worth** restricted to **Liquid** **Accounts** — the money available for day-to-day budgeting. Excludes Illiquid **Assets** and **Liabilities** (property value, mortgage, pensions, locked deposits) per each **Account**'s **Liquidity**.
_Avoid_: available funds, disposable income (a flow concept, not a stock), "net worth" bare (that is the unrestricted total).

**Distribution**:
A **Report** breaking down **Net movement** across one **AccountType** family — either **Income** ("where money came from") or **Expense** ("where money went") — over a **Reporting period**, rolled up the **Chart of accounts** tree and drillable one level at a time. Amounts are net: a refund credited to an **Expense** lowers that slice; a clawback lowers the **Income** slice. A subtree whose **Net movement** is net-negative in the period is excluded from the part-of-whole rendering and surfaced as a note rather than drawn as a slice.
_Avoid_: category breakdown, spending by category (category is banned — the slices are **Income** / **Expense** **Accounts**).

**Money flow**:
A **Report** depicting the whole ledger's in/out picture over a **Reporting period** as a single-hub flow diagram. Every **Account** contributes exactly one flow sized by its **Net movement**, and its side is chosen by sign: money *in* on the source side (**Income**, plus balance-sheet **Accounts** that shrank — a drained savings **Account**, new borrowing on a card), money *out* on the exit side (**Expense**, plus balance-sheet **Accounts** that grew — savings, investments, debt paid down, cash left as a buffer). Sources and exits balance exactly by the double-entry identity `Σ Income − Σ Expense = Σ (balance-sheet Net movement)`. The **Income** / **Expense** sides render at full **Chart of accounts** depth (subtrees become intermediate nodes); balance-sheet **Accounts** render at top level (v1). A net-negative **Account** flips to the opposite side for that period.
_Avoid_: cash flow (a specific, loaded accounting statement), Sankey (the chart type, not the concept), in/out report.

**JournalEntryTemplate**:
A user-confirmed, **JournalEntry**-*shaped* pattern that drives the forward-looking **Projection** — *not* a **JournalEntry** and never posted to the ledger (the future is computed, never written). Pinned to exactly one **Liquid** balance-sheet **Account** (the bank-side leg — the checking or savings it moves money in or out of), carrying a **Cadence** (`Once` / `Monthly` / …), an expected signed **Money** amount, an optional **Counterparty**, and a matching key (SEPA `MandateId` / `SepaCreditorId` where present, else **Counterparty** + account + amount-band — see **Occurrence matching**). A *recurring item* is colloquially a JournalEntryTemplate with a repeating **Cadence**; a *planned one-off* (next year's insurance, a planned large purchase) is one with **Cadence** `Once`. Its real-world realizations are ordinary **JournalEntries**, never stored against the template but recognized at query time. The only new *stored* concept in the forward-looking feature; the ledger gains no columns.
_Avoid_: RecurringJournalEntry / recurring transaction (implies a posted, repeating **JournalEntry** — the rejected scheduled-postings model), scheduled entry (implies posting), budget / envelope (banned framing), bare "entry" (avoided alias for **JournalEntry** / **JournalLine**).

**Cadence**:
The repetition rhythm of a **JournalEntryTemplate**, a closed enum — `Once`, `Weekly`, `Monthly`, `Quarterly`, `Yearly`. `Monthly` (and the longer rhythms) anchor to a *nominal* day-of-month that places the expected charge on the **Projection** timeline; the real charge drifts (weekends, the 1st–3rd), so **Occurrence matching** tolerates a window around the nominal day rather than requiring an exact date. `Once` is not "no cadence" — it is the planned one-off case (a future insurance bill, a planned purchase) that the same entity captures. A template is open-ended by default with an optional **end date**; there is no occurrence-count limit (an end date covers it and reads clearer).
_Avoid_: schedule (implies a materialized table of postings — nothing is materialized), frequency (fine colloquially; **Cadence** is the canonical term), cron / RRULE (no general recurrence expression in v1).

**Occurrence matching**:
The *query-time* recognition that a posted **JournalEntry** is a real realization of a **JournalEntryTemplate** — so the **Projection** uses the actual amount for that period instead of the template's expected amount (the loan **Projection**'s "past = actuals, future = projection" split, generalized). Uses the template's layered key: SEPA `MandateId` / `SepaCreditorId` where the **BankTransaction** carried one (the precise case), else **Counterparty** + the pinned **Account** + an amount-band, narrowed to the template's **Cadence** window. *Nothing is stored on the ledger* — no FK from **JournalEntry** / **JournalLine** to the template; the match is recomputed each time the Projection runs (an optional explicit pin is a deliberately-deferred additive escape hatch if real-world ambiguity proves painful).
_Avoid_: linking / attachment (reserved for **Attach** and bank-transaction wiring), reconciliation (reserved for **ReconciliationStatus**), binding.

**Typical spend**:
The modeled baseline of *non-recurring* variable flow on a **Liquid** balance-sheet **Account**, projected forward to make a balance **Projection** realistic — the everyday card spend, groceries, and fuel that no **JournalEntryTemplate** captures. Computed per liquid **Account**, per month, over that account's *everyday-spend* flow — the **Expense**-leg movement only — *excluding* (a) any **JournalEntry** matched to a **JournalEntryTemplate** by **Occurrence matching** (those are the recurring skeleton — counting them here double-counts), (b) ad-hoc **Self-transfers** (unpredictable lump moves, not typical spend — recurring transfers are modeled as templates instead), and (c) all *non-recurring income* (windfalls — a bonus, refund, gift, or sale proceeds — are categorically not everyday spend; counting them makes the curve read wildly *high*, the mirror of the original "systematically too high" failure). Typical spend is therefore a one-sided everyday-*spend* figure (normally ≤ 0; a refund credited back to its **Expense** account legitimately nets it upward). *Derived, never stored*, like every other **Projection** input. The center is the **median** monthly everyday spend over a trailing **six-month** window (long enough that a single odd month is one-sixth, not one-third, of the sample); its **spread** is a *robust* dispersion (median absolute deviation) of those monthly figures, so one outlier barely moves it. Rendered as a **band**, never a single point — but the band widens as a **random walk** (√n), not linearly: each future month is a fresh draw around the median, so the half-width at month *n* is `±1 robust-sigma × √n`, a "typical range" cone rather than a worst-case fan. This replaces the earlier *min/max-of-three-months applied every month* model, which assumed the single best and single worst month both recur forever and so fanned the cone to absurd six-figure widths (see ADR-0033). The forward headline reads as *known commitments (templates) + Typical spend (baseline)*.
_Avoid_: budget / envelope / allowance (banned framing — this is a derived estimate, not an allocation the user sets), forecast (vague), average spend (it is a median, and the band matters as much as the center), discretionary spend (narrower — this is all non-recurring flow, not only discretionary).

**Outlook**:
The forward-looking section of the app — the missing third tense beside the present-tense **Dashboard** and the past-tense, date-ranged **Insights**. Hosts the portfolio-level liquid-balance **Projection**: the **JournalEntryTemplate** list (expected recurring payments and planned one-offs), a savings balance curve, and a month-by-month checking **Projection** (commitments + **Typical spend** band). Loan **Projections** stay on **Loan** detail; Outlook is the cash/liquid view and where templates are managed. Surfaced in the nav as "Outlook".
_Avoid_: forecast (vague — flagged on **Projection** too), budget (banned framing), planner / planning (implies the user allocates money; Outlook *derives* a future, it does not budget), future (bare, vague).

**Scenario**:
A what-if overlay on a **Projection** — the same computation re-run with hypothetical overrides, *ephemeral and never persisted* (the loan flavor's extra-repayment / rate levers; the liquid flavor's template levers). In **Outlook** v1 a Scenario supports three levers on **JournalEntryTemplate**s: toggle one off (cancel a subscription), add a hypothetical one (a new savings transfer), or adjust one's expected amount (a rent increase). Scaling the **Typical spend** band is a deliberately-deferred fourth lever (a statistical scale invites false precision; additive to add later since nothing is persisted). A Scenario never writes to the ledger or to any **JournalEntryTemplate** — it exists only for the duration of the computation that renders it.
_Avoid_: simulation (fine colloquially; **Scenario** is the canonical noun), draft / plan (implies something saved), forecast (vague).

## Relationships

- A **JournalEntry** owns two or more **JournalLines** whose amounts net to zero.
- A **Loan** owns one or more **Loan Parts**; a **Loan Part** belongs to exactly one **Loan**.
- A **Loan** is represented by one non-postable **Liability** **Account**; each of its **Loan Parts** by one postable child **Account** of that parent. A part's Account is either created fresh or *adopted* — an existing postable **Liability** leaf re-parented under the loan with its history intact.
- A **Loan** references its lender **Counterparty** (drives the Inbox's loan-payment hint) and exactly one postable **Expense** **Account** that receives all its interest.
- A **Loan** *may* reference one **Construction deposit** (an **Asset** **Account**), one postable **Income** **Account** for the **Deposit-interest offset**, and a single annual deposit rate — all three set together or not at all. The **Construction deposit** is *not* **Loan-managed**.
- A **Loan Part**'s Account (and the **Loan**'s parent Account) is **Loan-managed**: only loan-aware flows post to it.
- A **Loan Part** owns an ordered, effective-dated list of **Rate periods**.
- A **JournalLine** *may* reference one **Loan Part** (attribution, set only by loan-aware flows); principal lines are attributed intrinsically by their Account, interest lines explicitly.
- Each **JournalLine** references exactly one **Account**.
- A **JournalEntry** *may* reference one **Counterparty**; **JournalLines** do not reference **Counterparties**.
- A **Counterparty** is never an **Account** (this is an explicit departure from Firefly III, which models each payee as an expense/revenue account).
- Each **Account** has exactly one **AccountType**.
- **Accounts** form a tree via a nullable `Account.ParentAccountId` self-reference, to arbitrary depth; cycles are rejected (an **Account** may not become its own ancestor).
- Every **Account** in a subtree shares one **AccountType** and one **CurrencyCode** (the homogeneity rule; the currency half relaxes when multi-currency lands). **Liquidity** is exempt from the homogeneity rule — it may vary freely within a subtree.
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
- A **BankTransaction** references at most one **JournalEntry** via the nullable scalar FK `BankTransaction.JournalEntryId`. A **JournalEntry** is referenced by zero, one, or many **BankTransactions**; multiple references are reserved for self-transfers (every line on an own-**Account**, `CounterpartyId IS NULL`) and enforced in the service layer as part of the **Attach predicate**. Splits are modeled as multiple **JournalLines** within one **JournalEntry**, not as multiple **JournalEntries** sharing a **BankTransaction**.
- A **BankTransaction** is immutable in its bank-supplied fields once stored; the **JournalEntry** referenced by it is editable. The mutable surface on a **BankTransaction** is `JournalEntryId` (via **Attach** / **Detach**), the **Dismissed** metadata (`DismissedAt`, `DismissedReason`), and the **BankTransactionMetadata** set (rebuilt by re-extraction from `RawSource`). Re-imports are deduplicated by hash.
- A **JournalEntryTemplate** is pinned to exactly one **Liquid** balance-sheet **Account** (the bank-side leg) and *may* reference one **Counterparty**. It is *not* a **JournalEntry**, owns no **JournalLines**, and is never posted; it is the only new *stored* concept in the forward-looking feature, and the ledger (**JournalEntry** / **JournalLine**) gains no columns for it.
- A **JournalEntryTemplate** carries exactly one **Cadence** and an expected signed **Money** amount in its pinned **Account**'s **Currency**; a `Once`-**Cadence** template is a planned one-off, every other **Cadence** is a recurring item. Its expected amount is seeded from history on creation and changes only by explicit user edit (frozen-with-a-nudge — never silent drift).
- **Occurrence matching** relates a posted **JournalEntry** to a **JournalEntryTemplate** at *query time only*, via the layered key (SEPA `MandateId` / `SepaCreditorId`, else **Counterparty** + the pinned **Account** + amount-band, within the **Cadence** window). No FK exists on **JournalEntry** / **JournalLine** pointing at a template; an explicit pin is a deferred additive escape hatch.
- The liquid-flavor **Projection** reads every **JournalEntryTemplate** plus the **Typical spend** baseline for the **Liquid** **Accounts** in scope, anchored to current ledger balances. It is computed, never stored; a **Scenario** overlays ephemeral template overrides on that same computation and persists nothing.

## Flagged ambiguities

- "account" is used in everyday speech to mean both a ledger **Account** and a bank account. Inside the domain, **Account** always means the ledger account (debit-normal or credit-normal, with an **AccountType**); the banking product is a **BankAccount** (carries IBAN / account number / bank metadata). An **Account** may be linked to a **BankAccount** when it represents a real bank product. Relatedly, the **Account**'s human key is its **Code** (a chart-of-accounts number); the bank-side identifier on a **BankAccount** is its `AccountNumber`. These are different things — never call the ledger Account's **Code** an "account number".
- "transaction" is overloaded in everyday speech (DB transactions, bank-statement rows, payment-API events). Inside the domain, the bookkeeping event is a **JournalEntry**; the immutable record of a bank-statement row is a **BankTransaction**. "Transaction" as a bare term is avoided.
- "user" is an *access-control* concept, not a domain one. A user is a human login (`AspNetUsers` row) that gates entry to the app; multiple users share one ledger, and the ledger has no per-user data. A user is *not* a **Counterparty** (the real-world party on the other side of a **JournalEntry**), is *not* an **Account** (a ledger account), and is *not* the **AccountHolderName** on a **BankAccount** (which is statement-row metadata about whoever owns a bank product). When the codebase says "user" it always means the logged-in human; when it means a counterparty or account-holder, it says so explicitly.

The **JournalEntry** editing and deletion rules (the per-`JournalLine` `ReconciliationStatus` gate, what is mutable on a **BankTransaction**, and cascade/RESTRICT behavior) live in [ADR-0014](docs/adr/0014-journal-entry-editability.md).
