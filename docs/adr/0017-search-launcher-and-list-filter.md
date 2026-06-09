---
status: accepted
---

# Search: a Cmd-K launcher and per-list `?q=` filters

Search is two deliberately separate surfaces. A **Cmd-K launcher** queries `GET /api/search?q=...&take=5` and returns grouped slim hits: **Account** by `Name`, **Counterparty** by `Name`, **BankAccount** by `Iban`/`AccountNumber`/`CardIdentifier`/`BankName`/`AccountHolderName`, and **JournalEntry** by `Description`, plus static page labels — each section capped at 5 with a `TotalCount`. **BankTransaction** is intentionally not a launcher entity (reach it via its JournalEntry or the Inbox). A **list filter** (`?q=`) on the existing list endpoints matches **BankTransaction** on `Description + CounterpartyName` and **JournalEntry** on `Description + Counterparty.Name` (item (g), per the 2026-05-30 amendment — the launcher still keeps Counterparty as its own section and does not fold counterparty-matched entries into the JournalEntry section).

Every list endpoint returns a uniform `PagedOutput<T> { Items, TotalCount }` driving a sliding-window numbered paginator; `PAGE_SIZE = 50`.

We rejected unifying the two surfaces (the list filter needs filter-on-visible-list semantics for bulk-categorizing the Inbox with selection intact across pagination, which a flat results page breaks), query syntax in the box, and Postgres-specific `ILIKE`/trigram/FTS — plain `field.Contains(term)` is milliseconds at realistic volumes, and the single-provider deployment makes the case-sensitivity difference acceptable.
