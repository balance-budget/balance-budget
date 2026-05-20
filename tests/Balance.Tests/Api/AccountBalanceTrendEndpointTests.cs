using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class AccountBalanceTrendEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task GetTrend_defaults_to_three_months_and_ends_today()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/dashboard/account-balance-trend", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var trend = await response.Content.ReadFromJsonAsync<AccountBalanceTrendDto>();
        await Assert.That(trend).IsNotNull();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expectedStart = today.AddMonths(-3);

        await Assert.That(trend!.Range).IsEqualTo("ThreeMonths");
        await Assert.That(trend.PeriodStart).IsEqualTo(expectedStart);
        await Assert.That(trend.PeriodEnd).IsEqualTo(today);
        await Assert.That(trend.CurrencyCode).IsEqualTo("EUR");
    }

    [Test]
    public async Task GetTrend_returns_400_on_unknown_range()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/dashboard/account-balance-trend?range=2M", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetTrend_one_month_range_starts_a_month_back()
    {
        using var client = Factory.CreateClient();

        var trend = await GetTrendAsync(client, range: "1M");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.That(trend.Range).IsEqualTo("OneMonth");
        await Assert.That(trend.PeriodStart).IsEqualTo(today.AddMonths(-1));
        await Assert.That(trend.PeriodEnd).IsEqualTo(today);
    }

    [Test]
    public async Task GetTrend_one_year_range_starts_a_year_back()
    {
        using var client = Factory.CreateClient();

        var trend = await GetTrendAsync(client, range: "1Y");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.That(trend.Range).IsEqualTo("OneYear");
        await Assert.That(trend.PeriodStart).IsEqualTo(today.AddYears(-1));
        await Assert.That(trend.PeriodEnd).IsEqualTo(today);
    }

    [Test]
    public async Task GetTrend_asset_with_opening_balance_and_no_activity_renders_flat_line()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var asset = await CreateAccountAsync(
            client,
            $"Trend-Flat-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var equity = await CreateAccountAsync(
            client,
            $"Trend-Flat-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        // Opening balance dated well before any TrendRange window.
        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(asset.Id, 500_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -500_000L, null),
            ]
        );

        var trend = await GetTrendAsync(client, range: "1M", currency: currency);

        var series = SeriesFor(trend, asset.Id);
        await Assert.That(series).IsNotNull();
        await Assert.That(series!.Points.Count).IsGreaterThan(0);
        // First point should be at periodStart, last at today.
        await Assert.That(series.Points[0].Date).IsEqualTo(trend.PeriodStart);
        await Assert.That(series.Points[^1].Date).IsEqualTo(trend.PeriodEnd);
        // Every point sits at the opening balance — no in-window activity.
        foreach (var point in series.Points)
        {
            await Assert.That(point.Balance!.Amount).IsEqualTo(500_000L);
            await Assert.That(point.Balance.CurrencyCode).IsEqualTo(currency);
        }
    }

    [Test]
    public async Task GetTrend_asset_with_mid_period_entry_jumps_on_that_date()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var asset = await CreateAccountAsync(
            client,
            $"Trend-Jump-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var equity = await CreateAccountAsync(
            client,
            $"Trend-Jump-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        // Opening balance long before the window.
        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(asset.Id, 100_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -100_000L, null),
            ]
        );

        // Mid-window deposit.
        var midDate = today.AddDays(-15);
        await PostJournalEntryAsync(
            client,
            midDate,
            [
                new CreateJournalLineRequestDto(asset.Id, 50_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -50_000L, null),
            ]
        );

        var trend = await GetTrendAsync(client, range: "1M", currency: currency);

        var series = SeriesFor(trend, asset.Id);
        await Assert.That(series).IsNotNull();

        var beforeJump = series!.Points.Single(p => p.Date == midDate.AddDays(-1));
        var atJump = series.Points.Single(p => p.Date == midDate);
        var lastPoint = series.Points[^1];

        await Assert.That(beforeJump.Balance!.Amount).IsEqualTo(100_000L);
        await Assert.That(atJump.Balance!.Amount).IsEqualTo(150_000L);
        await Assert.That(lastPoint.Balance!.Amount).IsEqualTo(150_000L);
    }

    [Test]
    public async Task GetTrend_emits_one_series_per_matching_asset_with_equal_length_points()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var assetA = await CreateAccountAsync(
            client,
            $"Trend-Multi-A-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var assetB = await CreateAccountAsync(
            client,
            $"Trend-Multi-B-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var equity = await CreateAccountAsync(
            client,
            $"Trend-Multi-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(assetA.Id, 10_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -10_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(assetB.Id, 20_000L, null),
                new CreateJournalLineRequestDto(equity.Id, -20_000L, null),
            ]
        );

        var trend = await GetTrendAsync(client, range: "1M", currency: currency);

        var seriesA = SeriesFor(trend, assetA.Id);
        var seriesB = SeriesFor(trend, assetB.Id);

        await Assert.That(seriesA).IsNotNull();
        await Assert.That(seriesB).IsNotNull();
        await Assert.That(seriesA!.Points.Count).IsEqualTo(seriesB!.Points.Count);
    }

    [Test]
    public async Task GetTrend_excludes_liability_accounts()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var liability = await CreateAccountAsync(
            client,
            $"Trend-Liab-{Guid.NewGuid():N}",
            "Liability",
            currency
        );
        var equity = await CreateAccountAsync(
            client,
            $"Trend-Liab-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(equity.Id, 150_000L, null),
                new CreateJournalLineRequestDto(liability.Id, -150_000L, null),
            ]
        );

        var trend = await GetTrendAsync(client, range: "1M", currency: currency);

        await Assert.That(SeriesFor(trend, liability.Id)).IsNull();
    }

    [Test]
    public async Task GetTrend_excludes_assets_in_a_different_currency()
    {
        using var client = Factory.CreateClient();
        var currencyA = await CreateIsolatedCurrencyAsync(client);
        var currencyB = await CreateIsolatedCurrencyAsync(client);

        var assetA = await CreateAccountAsync(
            client,
            $"Trend-CurA-{Guid.NewGuid():N}",
            "Asset",
            currencyA
        );
        var assetB = await CreateAccountAsync(
            client,
            $"Trend-CurB-{Guid.NewGuid():N}",
            "Asset",
            currencyB
        );
        var equityA = await CreateAccountAsync(
            client,
            $"Trend-CurA-Equity-{Guid.NewGuid():N}",
            "Equity",
            currencyA
        );
        var equityB = await CreateAccountAsync(
            client,
            $"Trend-CurB-Equity-{Guid.NewGuid():N}",
            "Equity",
            currencyB
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(assetA.Id, 1_000L, null),
                new CreateJournalLineRequestDto(equityA.Id, -1_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2020, 1, 1),
            [
                new CreateJournalLineRequestDto(assetB.Id, 2_000L, null),
                new CreateJournalLineRequestDto(equityB.Id, -2_000L, null),
            ]
        );

        var trend = await GetTrendAsync(client, range: "1M", currency: currencyA);

        await Assert.That(SeriesFor(trend, assetA.Id)).IsNotNull();
        await Assert.That(SeriesFor(trend, assetB.Id)).IsNull();
    }

    [Test]
    public async Task GetTrend_omits_assets_with_zero_opening_and_no_activity()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var silentAsset = await CreateAccountAsync(
            client,
            $"Trend-Silent-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        var trend = await GetTrendAsync(client, range: "1M", currency: currency);

        await Assert.That(SeriesFor(trend, silentAsset.Id)).IsNull();
    }

    private static AccountTrendSeriesDto? SeriesFor(AccountBalanceTrendDto trend, Guid accountId) =>
        trend.Series.FirstOrDefault(s => s.AccountId == accountId);

    private static async Task<AccountBalanceTrendDto> GetTrendAsync(
        HttpClient client,
        string? range = null,
        string? currency = null
    )
    {
        var query = new List<string>();
        if (range is not null)
        {
            query.Add($"range={range}");
        }
        if (currency is not null)
        {
            query.Add($"currency={currency}");
        }
        var path =
            query.Count == 0
                ? "/api/dashboard/account-balance-trend"
                : $"/api/dashboard/account-balance-trend?{string.Join("&", query)}";

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        var trend = await response.Content.ReadFromJsonAsync<AccountBalanceTrendDto>();
        return trend!;
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

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
        string currencyCode = "EUR"
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

    private static async Task PostJournalEntryAsync(
        HttpClient client,
        DateOnly date,
        IReadOnlyList<CreateJournalLineRequestDto> lines
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: date,
            Description: null,
            BankTransactionId: null,
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

internal sealed record AccountBalanceTrendDto(
    IReadOnlyList<AccountTrendSeriesDto> Series,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Range,
    string CurrencyCode
);

internal sealed record AccountTrendSeriesDto(
    Guid AccountId,
    string AccountName,
    IReadOnlyList<TrendPointDto> Points
);

internal sealed record TrendPointDto(DateOnly Date, MoneyDto? Balance);
