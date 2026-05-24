using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class ListJournalEntriesShapeTests : EndpointsTestsBase
{
    [Test]
    public async Task List_row_carries_projection_fields_for_simple_expense_entry()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"List-Exp-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"List-Exp-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 1),
            [
                new CreateJournalLineRequestDto(checking.Id, -4_000L, null),
                new CreateJournalLineRequestDto(groceries.Id, 4_000L, null),
            ]
        );

        var row = await GetRowForCurrencyAsync(client, currency);

        await Assert.That(row.LineCount).IsEqualTo(2);
        await Assert.That(row.IsTransfer).IsFalse();
        await Assert.That(row.NetWorthChange.Amount).IsEqualTo(-4_000L);
        await Assert.That(row.NetWorthChange.CurrencyCode).IsEqualTo(currency);
        await Assert.That(row.GrossMagnitude.Amount).IsEqualTo(4_000L);
        await Assert.That(row.IsSimplifiable).IsTrue();
        await Assert.That(row.FromLegs.Count).IsEqualTo(1);
        await Assert.That(row.FromLegs[0].AccountId).IsEqualTo(checking.Id);
        await Assert.That(row.FromLegs[0].AccountName).IsEqualTo(checking.Name);
        await Assert.That(row.ToLegs.Count).IsEqualTo(1);
        await Assert.That(row.ToLegs[0].AccountId).IsEqualTo(groceries.Id);
        await Assert.That(row.ToLegs[0].AccountName).IsEqualTo(groceries.Name);
    }

    [Test]
    public async Task List_row_marks_transfer_when_net_worth_change_is_zero()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"List-Tr-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var savings = await CreateAccountAsync(
            client,
            $"List-Tr-Save-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 2),
            [
                new CreateJournalLineRequestDto(checking.Id, -100_000L, null),
                new CreateJournalLineRequestDto(savings.Id, 100_000L, null),
            ]
        );

        var row = await GetRowForCurrencyAsync(client, currency);

        await Assert.That(row.IsTransfer).IsTrue();
        await Assert.That(row.NetWorthChange.Amount).IsEqualTo(0L);
        await Assert.That(row.GrossMagnitude.Amount).IsEqualTo(100_000L);
    }

    [Test]
    public async Task List_row_marks_split_not_simplifiable_when_multi_source_multi_destination()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var salary = await CreateAccountAsync(
            client,
            $"List-MM-Sal-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var bonus = await CreateAccountAsync(
            client,
            $"List-MM-Bon-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var tax = await CreateAccountAsync(
            client,
            $"List-MM-Tax-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var checking = await CreateAccountAsync(
            client,
            $"List-MM-Chk-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 3),
            [
                new CreateJournalLineRequestDto(salary.Id, -300_000L, null),
                new CreateJournalLineRequestDto(bonus.Id, -50_000L, null),
                new CreateJournalLineRequestDto(tax.Id, 100_000L, null),
                new CreateJournalLineRequestDto(checking.Id, 250_000L, null),
            ]
        );

        var row = await GetRowForCurrencyAsync(client, currency);

        await Assert.That(row.LineCount).IsEqualTo(4);
        await Assert.That(row.IsSimplifiable).IsFalse();
        await Assert.That(row.FromLegs.Count).IsEqualTo(0);
        await Assert.That(row.ToLegs.Count).IsEqualTo(0);
        await Assert.That(row.NetWorthChange.Amount).IsEqualTo(250_000L);
        await Assert.That(row.GrossMagnitude.Amount).IsEqualTo(350_000L);
    }

    [Test]
    public async Task List_row_includes_counterparty_name_when_set()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"List-CP-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"List-CP-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var counterpartyName = $"Albert Heijn {Guid.NewGuid():N}";
        var counterparty = await CreateCounterpartyAsync(client, counterpartyName);

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 4),
            [
                new CreateJournalLineRequestDto(checking.Id, -4_250L, null),
                new CreateJournalLineRequestDto(groceries.Id, 4_250L, null),
            ],
            counterpartyId: counterparty.Id
        );

        var row = await GetRowForCurrencyAsync(client, currency);

        await Assert.That(row.CounterpartyId).IsEqualTo(counterparty.Id);
        await Assert.That(row.CounterpartyName).IsEqualTo(counterpartyName);
    }

    [Test]
    public async Task List_row_counterparty_name_is_null_when_unset()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"List-NoCP-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"List-NoCP-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 5),
            [
                new CreateJournalLineRequestDto(checking.Id, -100L, null),
                new CreateJournalLineRequestDto(groceries.Id, 100L, null),
            ]
        );

        var row = await GetRowForCurrencyAsync(client, currency);

        await Assert.That(row.CounterpartyId).IsNull();
        await Assert.That(row.CounterpartyName).IsNull();
    }

    [Test]
    public async Task List_row_for_split_simplifiable_lists_all_destinations_in_to_legs()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"List-Sp-Chk-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"List-Sp-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var household = await CreateAccountAsync(
            client,
            $"List-Sp-Hous-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 6),
            [
                new CreateJournalLineRequestDto(checking.Id, -10_000L, null),
                new CreateJournalLineRequestDto(groceries.Id, 6_000L, null),
                new CreateJournalLineRequestDto(household.Id, 4_000L, null),
            ]
        );

        var row = await GetRowForCurrencyAsync(client, currency);

        await Assert.That(row.IsSimplifiable).IsTrue();
        await Assert.That(row.FromLegs.Count).IsEqualTo(1);
        await Assert.That(row.FromLegs[0].AccountId).IsEqualTo(checking.Id);
        await Assert.That(row.ToLegs.Count).IsEqualTo(2);
        var toIds = row.ToLegs.Select(l => l.AccountId).ToHashSet();
        await Assert.That(toIds.Contains(groceries.Id)).IsTrue();
        await Assert.That(toIds.Contains(household.Id)).IsTrue();
    }

    private static async Task<JournalEntryRowDto> GetRowForCurrencyAsync(
        HttpClient client,
        string currencyCode
    )
    {
        using var response = await client.GetAsync(
            new Uri("/api/journal-entries?take=200", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<IReadOnlyList<JournalEntryRowDto>>();
        await Assert.That(rows).IsNotNull();
        var matching = rows!
            .Where(r =>
                r.NetWorthChange.CurrencyCode == currencyCode
                || r.GrossMagnitude.CurrencyCode == currencyCode
            )
            .ToList();
        await Assert.That(matching.Count).IsGreaterThanOrEqualTo(1);
        return matching[0];
    }

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
        string currencyCode
    )
    {
        var req = new CreateAccountRequestDto(name, accountType, currencyCode);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task<CounterpartyDto> CreateCounterpartyAsync(
        HttpClient client,
        string name
    )
    {
        var req = new CreateCounterpartyRequestDto(name);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/counterparties", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CounterpartyDto>();
        return dto!;
    }

    private static async Task PostJournalEntryAsync(
        HttpClient client,
        DateOnly date,
        IReadOnlyList<CreateJournalLineRequestDto> lines,
        Guid? counterpartyId = null
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: date,
            Description: null,
            BankTransactionId: null,
            CounterpartyId: counterpartyId,
            Lines: lines
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateIsolatedCurrencyAsync(HttpClient client)
    {
        var code = ("Z" + Guid.NewGuid().ToString("N")[..4]).ToUpperInvariant();
        var request = new CreateCurrencyRequestDto(code, $"Test {code}", 2, null);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        return code;
    }
}

internal sealed record JournalEntryRowDto(
    Guid Id,
    DateOnly Date,
    string? Description,
    Guid? BankTransactionId,
    Guid? CounterpartyId,
    string? CounterpartyName,
    int LineCount,
    bool IsTransfer,
    MoneyDto NetWorthChange,
    MoneyDto GrossMagnitude,
    bool IsSimplifiable,
    IReadOnlyList<JournalEntryLegSummaryDto> FromLegs,
    IReadOnlyList<JournalEntryLegSummaryDto> ToLegs,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

internal sealed record JournalEntryLegSummaryDto(Guid AccountId, string AccountName);
