---
status: accepted (supersedes ADR-0026; amends ADR-0012)
---

# Construction deposit is a real savings account: interest credited in, then settled against the loan payment

A **Construction deposit** (Dutch *bouwdepot*) behaves like a savings account, not a passive reference: the lender pays interest *into* it each month (Dutch *Rentevergoeding*), that interest sits in the balance and compounds, and one period later the lender withdraws it (Dutch *Verrekening i.v.m. maandbedrag*) to fund part of the mortgage payment. ADR-0026 modeled this as a single fictional **Income** offset line inside the **Loan payment** that was "consumed as compensation" and never touched the deposit balance — which cannot reproduce the compounding or the one-period arrears, and contradicts the imported deposit statement. We replace it with the two real flows the bank actually books.

## What we record

Each construction month produces **two JournalEntries** from **three** imported **BankTransaction**s. The bouwdepot statement is imported like any other account, keyed by its non-IBAN `AccountNumber` (which equals the loan number), using `BankAccountType.Savings` (the only type whose CHECK permits a bare `AccountNumber`, so no schema change). The extractor is keyed by the mortgage-servicing platform that *produces* the layout, not the consumer lender — `Balance.Integration.Stater`, `ImporterKey = "Stater.ConstructionDeposit"` — because many Dutch banks issue byte-identical statements off Stater (consistent with ADR-0034's "logical importer identity, not a layout version"); the account's own `BankName` still carries the actual lender. The three row kinds:

- **Deposit-interest credit** (Entry A) — from **Categorizing** the *Rentevergoeding* row: debit the **Construction deposit** (**Asset**), credit the **Loan**'s deposit-interest **Income** account. Amount = `deposit balance at period start × monthly deposit rate`. The interest lands in the balance, so next period's interest is computed on a base that still includes it (compounding).
- **Loan payment** (Entry B) — from **Categorizing** the checking **net debit** row: the usual per-part interest **Expense** and principal lines, plus a **Deposit settlement** funding leg (a credit to the **Construction deposit**) equal to the *previous* period's credit (one period in arrears). The cash (bank) leg is therefore `gross + principal − settlement`, matching the reduced debit the bank collects. The settlement leg is created `Uncleared`; the deposit statement's *Verrekening* row **Attaches** to it.

Draws (*Uitbetaling*) remain ordinary categorized activity (disbursement to a contractor, or a **Self-transfer** reimbursement into Checking). The forward **Projection** stays rough and local (ADR-0027): the "current payment" headline nets the next settlement off *today's* deposit balance × monthly rate and reverts to gross at €0; it does not forecast draws (unforecastable) or simulate the deposit balance forward.

## Recognizing a loan payment for the Attach relaxation

A `JournalEntry` has no loan reference; the only loan signals are `JournalLine.LoanPartId` and the lender `CounterpartyId`. The generalized Attach relaxes guards **only when both**: the uniquely-matching `Uncleared` line is on an Account some `Loan` references as its `ConstructionDepositAccountId`, **and** the JE carries at least one `LoanPartId`-attributed line. This is purely structural (no `EntryKind` marker) and physically cannot loosen attach onto anything but a construction-deposit settlement leg. The settlement amount pre-fills from the *actual posted prior-month Deposit-interest credit* (ledger truth, matches the `Verrekening` to the cent), falling back to `prior balance × monthly rate` only when none is posted yet (first month, or credit not categorized). The `Rentevergoeding` itself stays a plain Categorize (its Income counter-account is filled by the existing last-used-account heuristic) — no new loan-aware trigger.

## Amendment to ADR-0012 (Attach)

The **Deposit settlement** makes a **Loan payment** a **JournalEntry** referenced by two **BankTransactions** (the checking net debit that created it, and the deposit *Verrekening* that attaches), even though it is *not* a self-transfer (it has an interest **Expense** line and a lender **Counterparty**). We therefore generalize the **Attach predicate**: when the candidate JE is a construction-phase **Loan payment**, drop guards **#6** (every line on an own-Account) and **#7** (`CounterpartyId IS NULL`), keeping **#2** (a uniquely-matching `Uncleared` own-Account line), **#4** (7-day window), and **#5** (currency). The relaxation is **scoped to loan payments** only — every other JE keeps the strict self-transfer guard. (Rule **#3**, the counterparty-IBAN match, is not enforced in code today and cannot apply anyway: the *Verrekening* row carries no counterparty account number.)

## Considered options

- **Keep the netted offset (ADR-0026).** Rejected: it cannot represent compounding or the one-period arrears, needed a hand-added "offset outlives the deposit by one period" special case, and fights the imported statement.
- **A single netted funding leg with no interest-credit entry**, or generating the credit via a Quartz job. Rejected: the two flows are one economic event best kept atomic; a job drifts and can double-post. The credit is a real statement row, so it is categorized like any other.
- **Model the settlement as a self-transfer deposit → checking**, then fund the loan payment entirely from checking. Rejected: no such transfer appears on any statement; the money goes straight from the deposit to the monthly amount.
- **Generalize Attach globally** rather than scoping to loan payments. Rejected: needlessly widens wrong-attach surface across the whole ledger to solve one narrow case.
- **Full forward simulation of the deposit balance.** Rejected: draws are invoice-driven and unforecastable, so the curve would be fiction dressed as precision (ADR-0027).
