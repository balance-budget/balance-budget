# Typed IDs over UUIDv7

Every entity's primary key is a strongly-typed `readonly record struct` over a UUIDv7 `Guid` — e.g. `record struct AccountId(Guid Value)`. IDs are generated client-side via `Guid.CreateVersion7()`, whose time-sortable prefix keeps clustered indexes happy on Postgres. The structs and their EF Core / `System.Text.Json` converters are emitted by a source generator (e.g. `StronglyTypedId`); `BaseEntity` becomes generic as `BaseEntity<TId>`. `Currency` keeps its natural string key, wrapped as `record struct CurrencyCode(string Value)`.

We rejected `int` identity columns because swapping e.g. an `AccountId` for a `CounterpartyId` in cross-table FKs is a real bug class, sequential IDs leak scale through public URLs, and they foreclose future device-sync. Raw `Guid` was rejected because it fixes identity but not parameter-swap safety. UUIDv7 beats UUIDv4 because random GUIDs make poor clustered-index keys on Postgres.
