using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class AccountRegisterEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task GetRegister_for_unknown_account_returns_404()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{Guid.NewGuid()}/register", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRegister_for_account_with_no_lines_returns_empty_list()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(
            client,
            $"Reg-Empty-{Guid.NewGuid():N}",
            "Asset",
            "EUR"
        );

        var rows = await GetRegisterAsync(client, account.Id);

        await Assert.That(rows).IsNotNull();
        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetRegister_for_debit_normal_account_uses_raw_sign()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-DN-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Reg-DN-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 10),
            [
                new CreateJournalLineRequestDto(checking.Id, 250_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -250_000L, null),
            ]
        );

        var rows = await GetRegisterAsync(client, checking.Id);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Amount.Amount).IsEqualTo(250_000L);
        await Assert.That(rows[0].Amount.CurrencyCode).IsEqualTo(currency);
    }

    [Test]
    public async Task GetRegister_for_credit_normal_account_flips_sign()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var visa = await CreateAccountAsync(
            client,
            $"Reg-CN-Visa-{Guid.NewGuid():N}",
            "Liability",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Reg-CN-Groceries-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        // Visa charge: liability credited (-amount), expense debited (+amount).
        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 11),
            [
                new CreateJournalLineRequestDto(visa.Id, -10_000L, null),
                new CreateJournalLineRequestDto(groceries.Id, 10_000L, null),
            ]
        );

        var rows = await GetRegisterAsync(client, visa.Id);

        await Assert.That(rows.Count).IsEqualTo(1);
        // Liability is credit-normal: balance contribution and focal-signed amount flip.
        // Raw -10_000 → focal-signed +10_000 (liability went up, "money in" to the focal
        // liability account per the focal-account convention).
        await Assert.That(rows[0].Amount.Amount).IsEqualTo(10_000L);
    }

    [Test]
    public async Task GetRegister_for_simple_two_leg_entry_has_one_offsetting_leg()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-SL-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var rent = await CreateAccountAsync(
            client,
            $"Reg-SL-Rent-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 12),
            [
                new CreateJournalLineRequestDto(checking.Id, -120_000L, null),
                new CreateJournalLineRequestDto(rent.Id, 120_000L, null),
            ]
        );

        var rows = await GetRegisterAsync(client, checking.Id);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Counter.Count).IsEqualTo(1);
        await Assert.That(rows[0].Counter[0].AccountId).IsEqualTo(rent.Id);
        await Assert.That(rows[0].Counter[0].AccountName).IsEqualTo(rent.Name);
        await Assert.That(rows[0].Counter[0].Amount.Amount).IsEqualTo(120_000L);
        await Assert.That(rows[0].Counter[0].Amount.CurrencyCode).IsEqualTo(currency);
    }

    [Test]
    public async Task GetRegister_for_split_entry_returns_multiple_offsetting_legs()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-SP-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Reg-SP-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var household = await CreateAccountAsync(
            client,
            $"Reg-SP-House-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        // €100 purchase split 60/40 across Groceries and Household.
        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 13),
            [
                new CreateJournalLineRequestDto(checking.Id, -10_000L, null),
                new CreateJournalLineRequestDto(groceries.Id, 6_000L, null),
                new CreateJournalLineRequestDto(household.Id, 4_000L, null),
            ]
        );

        var rows = await GetRegisterAsync(client, checking.Id);

        await Assert.That(rows.Count).IsEqualTo(1);
        // Focal amount stays as the sum on the focal account (single line, -10_000 raw,
        // Asset is debit-normal so focal-signed = -10_000). This is what a bank
        // statement would show for the same entry.
        await Assert.That(rows[0].Amount.Amount).IsEqualTo(-10_000L);
        await Assert.That(rows[0].Counter.Count).IsEqualTo(2);
        var byAccount = rows[0].Counter.ToDictionary(c => c.AccountId, c => c.Amount.Amount);
        await Assert.That(byAccount[groceries.Id]).IsEqualTo(6_000L);
        await Assert.That(byAccount[household.Id]).IsEqualTo(4_000L);
    }

    [Test]
    public async Task GetRegister_orders_rows_by_date_desc_then_entry_id_desc()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-Ord-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Reg-Ord-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        // Three entries on three distinct dates; expect newest first.
        var dates = new[]
        {
            new DateOnly(2026, 5, 14),
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 5, 16),
        };
        foreach (var date in dates)
        {
            await PostJournalEntryAsync(
                client,
                date,
                [
                    new CreateJournalLineRequestDto(checking.Id, 100L, null),
                    new CreateJournalLineRequestDto(salary.Id, -100L, null),
                ]
            );
        }

        var rows = await GetRegisterAsync(client, checking.Id);

        await Assert.That(rows.Count).IsEqualTo(3);
        await Assert.That(rows[0].Date).IsEqualTo(new DateOnly(2026, 5, 16));
        await Assert.That(rows[1].Date).IsEqualTo(new DateOnly(2026, 5, 15));
        await Assert.That(rows[2].Date).IsEqualTo(new DateOnly(2026, 5, 14));
    }

    [Test]
    public async Task GetRegister_pagination_skip_and_take_slice_the_ordered_set()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-Pg-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Reg-Pg-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        var dates = new[]
        {
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 2),
            new DateOnly(2026, 5, 3),
            new DateOnly(2026, 5, 4),
            new DateOnly(2026, 5, 5),
        };
        foreach (var date in dates)
        {
            await PostJournalEntryAsync(
                client,
                date,
                [
                    new CreateJournalLineRequestDto(checking.Id, 100L, null),
                    new CreateJournalLineRequestDto(salary.Id, -100L, null),
                ]
            );
        }

        var page = await GetRegisterAsync(client, checking.Id, skip: 1, take: 2);

        // Ordered newest first: 5, 4, 3, 2, 1. Skip 1 → starts at 4. Take 2 → 4, 3.
        await Assert.That(page.Count).IsEqualTo(2);
        await Assert.That(page[0].Date).IsEqualTo(new DateOnly(2026, 5, 4));
        await Assert.That(page[1].Date).IsEqualTo(new DateOnly(2026, 5, 3));
    }

    [Test]
    public async Task GetRegister_take_zero_returns_empty_list()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-T0-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Reg-T0-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 6),
            [
                new CreateJournalLineRequestDto(checking.Id, 100L, null),
                new CreateJournalLineRequestDto(salary.Id, -100L, null),
            ]
        );

        var rows = await GetRegisterAsync(client, checking.Id, skip: 0, take: 0);

        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetRegister_skip_past_end_returns_empty_list()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-Sk-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Reg-Sk-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 7),
            [
                new CreateJournalLineRequestDto(checking.Id, 100L, null),
                new CreateJournalLineRequestDto(salary.Id, -100L, null),
            ]
        );

        var rows = await GetRegisterAsync(client, checking.Id, skip: 50, take: 10);

        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetRegister_includes_counterparty_name_when_set()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Reg-CP-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Reg-CP-Groc-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var counterpartyName = $"Albert Heijn {Guid.NewGuid():N}";
        var counterparty = await CreateCounterpartyAsync(client, counterpartyName);

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 8),
            [
                new CreateJournalLineRequestDto(checking.Id, -4_250L, null),
                new CreateJournalLineRequestDto(groceries.Id, 4_250L, null),
            ],
            counterpartyId: counterparty.Id
        );

        var rows = await GetRegisterAsync(client, checking.Id);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].CounterpartyId).IsEqualTo(counterparty.Id);
        await Assert.That(rows[0].CounterpartyName).IsEqualTo(counterpartyName);
    }

    [Test]
    public async Task GetRegister_invalid_take_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(
            client,
            $"Reg-Inv-{Guid.NewGuid():N}",
            "Asset",
            "EUR"
        );

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{account.Id}/register?take=-1", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task<IReadOnlyList<RegisterRowDto>> GetRegisterAsync(
        HttpClient client,
        Guid accountId,
        int? skip = null,
        int? take = null
    )
    {
        var queryParts = new List<string>();
        if (skip is not null)
            queryParts.Add($"skip={skip}");
        if (take is not null)
            queryParts.Add($"take={take}");
        var query = queryParts.Count == 0 ? string.Empty : "?" + string.Join("&", queryParts);

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{accountId}/register{query}", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadPagedItemsAsync<RegisterRowDto>();
        return rows;
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
            CounterpartyId: counterpartyId,
            Lines: lines
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
    }

    // Same isolation trick as DashboardSummaryEndpointTests — share the integration-test
    // SQLite DB across the session but keep each test's accounts and journal lines on a
    // freshly-minted CurrencyCode so cross-class iteration doesn't bleed sums.
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

internal sealed record RegisterRowDto(
    Guid JournalEntryId,
    Guid JournalLineId,
    DateOnly Date,
    string? EntryDescription,
    Guid? CounterpartyId,
    string? CounterpartyName,
    string? LineDescription,
    string ReconciliationStatus,
    MoneyDto Amount,
    IReadOnlyList<RegisterRowCounterLegDto> Counter
);

internal sealed record RegisterRowCounterLegDto(
    Guid AccountId,
    string AccountName,
    MoneyDto Amount
);
