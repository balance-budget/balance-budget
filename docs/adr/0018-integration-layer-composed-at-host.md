# Integration layer composed at the host beside Services

Bank-statement importers live in their own `Balance.Integration.*` projects (today `Balance.Integration.Ing`) that **reference `Balance.Services`** and implement the Services-owned contract `IBankTransactionExtractor`. The host composes each with a third top-level call — `AddBalanceIntegrationIng()` in `Program.cs`, **beside** `AddBalanceServices` and `AddBalanceWeb`, not nested under Services.

The dependency direction forces this. `BankTransactionImportService` consumes `IEnumerable<IBankTransactionExtractor>`, so the contract belongs to Services; a concrete extractor needs Services' domain types, so the integration project must reference Services. If Services registered the extractors it would have to reference the integration project — a Services↔Integration cycle. The host is the only place that legitimately sees both. A new bank is therefore a new project plus one `AddBalanceIntegration<Bank>()` line, with no edit to Services.

We rejected folding extractors into Services (couples the bank-agnostic domain to each bank's CSV quirks) and inverting the contract so Services depends on integrations (the same cycle with arrows flipped).
