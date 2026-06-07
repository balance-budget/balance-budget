using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class DashboardSummaryEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task GetSummary_returns_period_in_current_calendar_month()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/dashboard/summary", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        await Assert.That(summary).IsNotNull();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expectedStart = new DateOnly(today.Year, today.Month, 1);
        var expectedEnd = expectedStart.AddMonths(1).AddDays(-1);

        await Assert.That(summary!.PeriodStart).IsEqualTo(expectedStart);
        await Assert.That(summary.PeriodEnd).IsEqualTo(expectedEnd);
        await Assert.That(summary.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(summary.NetWorth!.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(summary.IncomeMtd!.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(summary.ExpensesMtd!.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(summary.IncomeMtdPrior!.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(summary.ExpensesMtdPrior!.CurrencyCode).IsEqualTo("EUR");
    }

    [Test]
    public async Task GetSummary_NetWorth_is_assets_minus_liabilities()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var asset = await CreateAccountAsync(
            client,
            $"NW-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var liability = await CreateAccountAsync(
            client,
            $"NW-Liability-{Guid.NewGuid():N}",
            "Liability",
            currency
        );
        var equity = await CreateAccountAsync(
            client,
            $"NW-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 1, 1),
            [
                new CreateJournalLineRequestDto(asset.Id, 500_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -500_000L, null),
            ]
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 1, 1),
            [
                new CreateJournalLineRequestDto(equity.Id, 150_000L, null),
                new CreateJournalLineRequestDto(liability.Id, -150_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.NetWorth!.Amount).IsEqualTo(350_000L);
        await Assert.That(summary.NetWorth.CurrencyCode).IsEqualTo(currency);

        // No illiquid accounts in play, so the liquid figure matches the total.
        await Assert.That(summary.LiquidNetWorth!.Amount).IsEqualTo(350_000L);
        await Assert.That(summary.LiquidNetWorth.CurrencyCode).IsEqualTo(currency);
    }

    [Test]
    public async Task GetSummary_LiquidNetWorth_excludes_illiquid_accounts()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var checking = await CreateAccountAsync(
            client,
            $"LNW-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var home = await CreateAccountAsync(
            client,
            $"LNW-Home-{Guid.NewGuid():N}",
            "Asset",
            currency,
            isLiquid: false
        );
        var mortgage = await CreateAccountAsync(
            client,
            $"LNW-Mortgage-{Guid.NewGuid():N}",
            "Liability",
            currency,
            isLiquid: false
        );
        var equity = await CreateAccountAsync(
            client,
            $"LNW-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 1, 1),
            [
                new CreateJournalLineRequestDto(checking.Id, 500_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -500_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 1, 1),
            [
                new CreateJournalLineRequestDto(home.Id, 2_000_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -2_000_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 1, 1),
            [
                new CreateJournalLineRequestDto(equity.Id, 1_500_000L, null),
                new CreateJournalLineRequestDto(mortgage.Id, -1_500_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        // Net worth counts everything: 500k + 2M - 1.5M. Liquid net worth counts only the
        // liquid checking account — the home and mortgage are the user's illiquid world.
        await Assert.That(summary.NetWorth!.Amount).IsEqualTo(1_000_000L);
        await Assert.That(summary.LiquidNetWorth!.Amount).IsEqualTo(500_000L);
    }

    [Test]
    public async Task GetSummary_NetWorth_excludes_equity_income_and_expense_accounts()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var asset = await CreateAccountAsync(
            client,
            $"NW-Excl-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var income = await CreateAccountAsync(
            client,
            $"NW-Excl-Income-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var expense = await CreateAccountAsync(
            client,
            $"NW-Excl-Expense-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 2, 2),
            [
                new CreateJournalLineRequestDto(asset.Id, 100_000L, null),
                new CreateJournalLineRequestDto(income.Id, -100_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 2, 2),
            [
                new CreateJournalLineRequestDto(expense.Id, 25_000L, null),
                new CreateJournalLineRequestDto(asset.Id, -25_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        // Asset balance is 100_000 - 25_000 = 75_000. No liabilities. NetWorth = 75_000.
        // Income and Expense lines must not contribute.
        await Assert.That(summary.NetWorth!.Amount).IsEqualTo(75_000L);
    }

    [Test]
    public async Task GetSummary_IncomeMtd_sums_credits_to_income_accounts_this_month()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var salary = await CreateAccountAsync(
            client,
            $"MTD-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"MTD-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            today,
            [
                new CreateJournalLineRequestDto(asset.Id, 250_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -250_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.IncomeMtd!.Amount).IsEqualTo(250_000L);
    }

    [Test]
    public async Task GetSummary_ExpensesMtd_signs_as_money_out_this_month()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var groceries = await CreateAccountAsync(
            client,
            $"MTD-Groceries-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"MTD-AssetX-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            today,
            [
                new CreateJournalLineRequestDto(groceries.Id, 8_500L, null),
                new CreateJournalLineRequestDto(asset.Id, -8_500L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.ExpensesMtd!.Amount).IsEqualTo(-8_500L);
    }

    [Test]
    public async Task GetSummary_IncomeMtdPrior_sums_income_in_same_period_last_month()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = new DateOnly(priorPeriodEnd.Year, priorPeriodEnd.Month, 1);

        var salary = await CreateAccountAsync(
            client,
            $"SPLM-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"SPLM-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            priorPeriodStart,
            [
                new CreateJournalLineRequestDto(asset.Id, 120_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -120_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            priorPeriodEnd,
            [
                new CreateJournalLineRequestDto(asset.Id, 80_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -80_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.IncomeMtdPrior!.Amount).IsEqualTo(200_000L);
        await Assert.That(summary.IncomeMtd!.Amount).IsEqualTo(0L);
    }

    [Test]
    public async Task GetSummary_ExpensesMtdPrior_signs_as_money_out_in_same_period_last_month()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = new DateOnly(priorPeriodEnd.Year, priorPeriodEnd.Month, 1);

        var groceries = await CreateAccountAsync(
            client,
            $"SPLM-Groceries-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"SPLM-AssetX-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            priorPeriodStart,
            [
                new CreateJournalLineRequestDto(groceries.Id, 12_000L, null),
                new CreateJournalLineRequestDto(asset.Id, -12_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.ExpensesMtdPrior!.Amount).IsEqualTo(-12_000L);
    }

    [Test]
    public async Task GetSummary_PriorPeriod_is_zero_when_no_activity_in_splm_window()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var salary = await CreateAccountAsync(
            client,
            $"SPLM-Zero-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var groceries = await CreateAccountAsync(
            client,
            $"SPLM-Zero-Groceries-{Guid.NewGuid():N}",
            "Expense",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"SPLM-Zero-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        // Anchor far in the past so neither current month nor SPLM window contains it.
        await PostJournalEntryAsync(
            client,
            new DateOnly(2019, 7, 4),
            [
                new CreateJournalLineRequestDto(asset.Id, 5_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -5_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2019, 7, 4),
            [
                new CreateJournalLineRequestDto(groceries.Id, 3_000L, null),
                new CreateJournalLineRequestDto(asset.Id, -3_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.IncomeMtdPrior!.Amount).IsEqualTo(0L);
        await Assert.That(summary.ExpensesMtdPrior!.Amount).IsEqualTo(0L);
        await Assert.That(summary.IncomeMtdPrior.CurrencyCode).IsEqualTo(currency);
        await Assert.That(summary.ExpensesMtdPrior.CurrencyCode).IsEqualTo(currency);
    }

    [Test]
    public async Task GetSummary_PriorPeriod_excludes_entries_dated_after_splm_end()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var priorPeriodEnd = today.AddMonths(-1);
        var dayAfterPriorEnd = priorPeriodEnd.AddDays(1);

        var salary = await CreateAccountAsync(
            client,
            $"SPLM-After-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"SPLM-After-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            dayAfterPriorEnd,
            [
                new CreateJournalLineRequestDto(asset.Id, 99_999L, null),
                new CreateJournalLineRequestDto(salary.Id, -99_999L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.IncomeMtdPrior!.Amount).IsEqualTo(0L);
    }

    [Test]
    public async Task GetSummary_excludes_journal_entries_outside_current_month()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var salary = await CreateAccountAsync(
            client,
            $"OOM-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var asset = await CreateAccountAsync(
            client,
            $"OOM-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 6, 15),
            [
                new CreateJournalLineRequestDto(asset.Id, 999_999L, null),
                new CreateJournalLineRequestDto(salary.Id, -999_999L, null),
            ]
        );

        var summary = await GetSummaryAsync(client, currency);

        await Assert.That(summary.IncomeMtd!.Amount).IsEqualTo(0L);
        await Assert.That(summary.ExpensesMtd!.Amount).IsEqualTo(0L);
        await Assert.That(summary.NetWorth!.Amount).IsEqualTo(999_999L);
    }

    // Mints a fresh CurrencyCode per test so each test's accounts and journal lines
    // are guaranteed not to overlap with any sibling test running in parallel against
    // the same integration-test database.
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

    private static async Task<DashboardSummaryDto> GetSummaryAsync(
        HttpClient client,
        string? currency = null
    )
    {
        var path = currency is null
            ? "/api/dashboard/summary"
            : $"/api/dashboard/summary?currency={currency}";
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        return summary!;
    }

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
        string currencyCode = "EUR",
        bool isLiquid = true
    )
    {
        var req = new CreateAccountRequestDto(name, accountType, currencyCode)
        {
            IsLiquid = isLiquid,
        };
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task PostJournalEntryAsync(
        HttpClient client,
        DateOnly date,
        IReadOnlyList<CreateJournalLineRequestDto> lines
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: date,
            Description: null,
            CounterpartyId: null,
            Lines: lines
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
    }
}

internal sealed record DashboardSummaryDto(
    MoneyDto? NetWorth,
    MoneyDto? LiquidNetWorth,
    MoneyDto? IncomeMtd,
    MoneyDto? ExpensesMtd,
    MoneyDto? IncomeMtdPrior,
    MoneyDto? ExpensesMtdPrior,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CurrencyCode
);
