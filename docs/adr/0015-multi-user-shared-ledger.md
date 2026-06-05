# Multi-user with a single shared ledger

The app supports multiple human logins but exactly one ledger: every authenticated user has identical, full access to every **Account**, **JournalEntry**, **BankAccount**, **Counterparty**, and **BankTransaction**. There is no `OwnerId` / `UserId` / `TenantId` column on any domain entity, no per-user query filter, and no role hierarchy. The intended deployment is a self-hosted household tool; identity exists at the access boundary only — it gates whether a request is admitted, never which rows it sees.

We rejected per-user data isolation, a single-owner login (two humans want separate revocable passwords), per-entity attribution (`CreatedByUserId`), and an `Admin` role — each adds machinery the household model never uses. Attribution and roles can be added later as additive, nullable migrations.

Two foot-gun guards stand in for the absent role system: you cannot disable yourself, and you cannot disable the last active user.
