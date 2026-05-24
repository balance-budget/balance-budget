using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

/// <summary>
/// Asserts that <c>GET /api/journal-entries/{id}</c> ships the same projection fields
/// as the list endpoint (per ADR-0008 and ADR-0012) plus the full <c>Lines</c> array,
/// with joined <c>AccountName</c> per line and <c>CounterpartyName</c> on the header.
/// The detail SPA page consumes this single response shape — projection + lines.
/// </summary>
internal sealed class JournalEntryDetailShapeTests : EndpointsTestsBase
{
    [Test]
    public async Task Detail_carries_projection_fields_for_simple_expense_entry()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Det-Exp-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Det-Exp-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 1),
            [
                new CreateJournalLineRequestDto(checking.Id, -4_000L, null),
                new CreateJournalLineRequestDto(groceries.Id, 4_000L, null),
            ]
        );

        var detail = await GetDetailAsync(client, created.Id);

        await Assert.That(detail.LineCount).IsEqualTo(2);
        await Assert.That(detail.IsTransfer).IsFalse();
        await Assert.That(detail.NetWorthChange.Amount).IsEqualTo(-4_000L);
        await Assert.That(detail.NetWorthChange.CurrencyCode).IsEqualTo(currency);
        await Assert.That(detail.GrossMagnitude.Amount).IsEqualTo(4_000L);
        await Assert.That(detail.IsSimplifiable).IsTrue();
        await Assert.That(detail.FromLegs.Count).IsEqualTo(1);
        await Assert.That(detail.FromLegs[0].AccountId).IsEqualTo(checking.Id);
        await Assert.That(detail.FromLegs[0].AccountName).IsEqualTo(checking.Name);
        await Assert.That(detail.ToLegs.Count).IsEqualTo(1);
        await Assert.That(detail.ToLegs[0].AccountId).IsEqualTo(groceries.Id);
        await Assert.That(detail.ToLegs[0].AccountName).IsEqualTo(groceries.Name);
    }

    [Test]
    public async Task Detail_marks_transfer_when_net_worth_change_is_zero()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Det-Tr-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var savings = await CreateAccountAsync(
            client,
            $"Det-Tr-Save-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 2),
            [
                new CreateJournalLineRequestDto(checking.Id, -100_000L, null),
                new CreateJournalLineRequestDto(savings.Id, 100_000L, null),
            ]
        );

        var detail = await GetDetailAsync(client, created.Id);

        await Assert.That(detail.IsTransfer).IsTrue();
        await Assert.That(detail.NetWorthChange.Amount).IsEqualTo(0L);
        await Assert.That(detail.GrossMagnitude.Amount).IsEqualTo(100_000L);
    }

    [Test]
    public async Task Detail_marks_split_not_simplifiable_when_multi_source_multi_destination()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var salary = await CreateAccountAsync(
            client,
            $"Det-MM-Sal-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var bonus = await CreateAccountAsync(
            client,
            $"Det-MM-Bon-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var tax = await CreateAccountAsync(
            client,
            $"Det-MM-Tax-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var checking = await CreateAccountAsync(
            client,
            $"Det-MM-Chk-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 3),
            [
                new CreateJournalLineRequestDto(salary.Id, -300_000L, null),
                new CreateJournalLineRequestDto(bonus.Id, -50_000L, null),
                new CreateJournalLineRequestDto(tax.Id, 100_000L, null),
                new CreateJournalLineRequestDto(checking.Id, 250_000L, null),
            ]
        );

        var detail = await GetDetailAsync(client, created.Id);

        await Assert.That(detail.LineCount).IsEqualTo(4);
        await Assert.That(detail.IsSimplifiable).IsFalse();
        await Assert.That(detail.FromLegs.Count).IsEqualTo(0);
        await Assert.That(detail.ToLegs.Count).IsEqualTo(0);
        await Assert.That(detail.NetWorthChange.Amount).IsEqualTo(250_000L);
        await Assert.That(detail.GrossMagnitude.Amount).IsEqualTo(350_000L);
    }

    [Test]
    public async Task Detail_includes_counterparty_name_when_set()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Det-CP-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Det-CP-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var counterpartyName = $"Albert Heijn {Guid.NewGuid():N}";
        var counterparty = await CreateCounterpartyAsync(client, counterpartyName);

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 4),
            [
                new CreateJournalLineRequestDto(checking.Id, -4_250L, null),
                new CreateJournalLineRequestDto(groceries.Id, 4_250L, null),
            ],
            counterpartyId: counterparty.Id
        );

        var detail = await GetDetailAsync(client, created.Id);

        await Assert.That(detail.CounterpartyId).IsEqualTo(counterparty.Id);
        await Assert.That(detail.CounterpartyName).IsEqualTo(counterpartyName);
    }

    [Test]
    public async Task Detail_counterparty_name_is_null_when_unset()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Det-NoCP-Check-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Det-NoCP-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 5),
            [
                new CreateJournalLineRequestDto(checking.Id, -100L, null),
                new CreateJournalLineRequestDto(groceries.Id, 100L, null),
            ]
        );

        var detail = await GetDetailAsync(client, created.Id);

        await Assert.That(detail.CounterpartyId).IsNull();
        await Assert.That(detail.CounterpartyName).IsNull();
    }

    [Test]
    public async Task Detail_includes_full_lines_array_with_joined_account_name()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Det-Lines-Chk-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Det-Lines-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 6),
            [
                new CreateJournalLineRequestDto(checking.Id, -500L, "from checking"),
                new CreateJournalLineRequestDto(groceries.Id, 500L, "to groceries"),
            ]
        );

        var detail = await GetDetailAsync(client, created.Id);

        await Assert.That(detail.Lines.Count).IsEqualTo(2);
        var checkingLine = detail.Lines.Single(l => l.AccountId == checking.Id);
        await Assert.That(checkingLine.AccountName).IsEqualTo(checking.Name);
        await Assert.That(checkingLine.Amount).IsEqualTo(-500L);
        await Assert.That(checkingLine.Description).IsEqualTo("from checking");
        var groceriesLine = detail.Lines.Single(l => l.AccountId == groceries.Id);
        await Assert.That(groceriesLine.AccountName).IsEqualTo(groceries.Name);
        await Assert.That(groceriesLine.Amount).IsEqualTo(500L);
    }

    [Test]
    public async Task Create_response_carries_projection_fields_and_joined_names()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Det-Cr-Chk-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Det-Cr-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var counterpartyName = $"Lidl {Guid.NewGuid():N}";
        var counterparty = await CreateCounterpartyAsync(client, counterpartyName);

        var created = await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 7, 7),
            [
                new CreateJournalLineRequestDto(checking.Id, -750L, null),
                new CreateJournalLineRequestDto(groceries.Id, 750L, null),
            ],
            counterpartyId: counterparty.Id
        );

        // Per ADR-0008 the Create/Update responses ship the same shape as Get so the
        // client can land the result straight into the detail cache.
        await Assert.That(created.CounterpartyName).IsEqualTo(counterpartyName);
        await Assert.That(created.IsTransfer).IsFalse();
        await Assert.That(created.NetWorthChange.Amount).IsEqualTo(-750L);
        await Assert.That(created.IsSimplifiable).IsTrue();
        await Assert.That(created.Lines.Count).IsEqualTo(2);
        await Assert.That(created.Lines.All(l => !string.IsNullOrEmpty(l.AccountName))).IsTrue();
    }

    private static async Task<JournalEntryDetailDto> GetDetailAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries/{id}", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDetailDto>();
        await Assert.That(dto).IsNotNull();
        return dto!;
    }

    private static async Task<JournalEntryDetailDto> PostJournalEntryAsync(
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
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDetailDto>();
        return dto!;
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

internal sealed record JournalEntryDetailDto(
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
    IReadOnlyList<JournalLineDetailDto> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

internal sealed record JournalLineDetailDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    long Amount,
    string ReconciliationStatus,
    string? Description
);
