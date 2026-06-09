# JournalEntry editability is gated per `JournalLine` by `ReconciliationStatus`

A `JournalLine` is fully editable iff its `ReconciliationStatus == Uncleared`; `Cleared` and `Reconciled` lines are frozen except for `Description`. The edit surface is a full-body `PUT /api/journal-entries/{id}` carrying the desired final state.

We rejected gating editability entry-wide on `ReconciliationStatus` (either direction either re-points a bank-confirmed line or freezes every imported entry once categorization completes) and folding `ReconciliationStatus` mutation into the PUT (flipping status belongs to the future reconciliation pass, which gets its own endpoint).

## Editing surface

- **Entry-level (editable):** `Date`, `Description`, `CounterpartyId`. `BankTransactionId` is immutable.
- **`Uncleared` line (fully editable):** `AccountId`, `Amount`, `Description`. The line may be removed and new lines added — the set of `Uncleared` lines is freely reshapeable. New lines get a server-assigned `Id` and default to `ReconciliationStatus.Uncleared`.
- **`Cleared` / `Reconciled` line (frozen):** only `Description` is editable; `AccountId` and `Amount` are matched by `Id` against the existing entry; the line cannot be removed.
- Editing never mutates `ReconciliationStatus` on existing lines.
- The server validates that every line with an `Id` whose current status `!= Uncleared` is unchanged in `AccountId`/`Amount`, and that line amounts sum to zero per `Currency`.
- **BankTransaction** is immutable in its bank-supplied fields. Its only mutable surfaces are `JournalEntryId` (via Attach/Detach, see ADR-0012), the **Dismissed** metadata, and the **BankTransactionMetadata** set (rebuilt by re-extraction).

## Deletion

- **JournalEntry** is deletable: **JournalLines** cascade, and referencing **BankTransactions** have `JournalEntryId` set null via `ON DELETE SET NULL` and return to the **Inbox**.
- **BankTransaction** is never deleted — **Dismiss** instead (reversible).
- **Account**, **Counterparty**, **BankAccount** are hard-deletable but blocked by FK when referenced. A non-postable parent **Account** with children cannot be deleted (FK `RESTRICT` on `ParentAccountId`).
- No `IsArchived` flag in v1.
