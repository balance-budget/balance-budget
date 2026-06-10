using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class AccountRegisterSummaryEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task GetSummary_for_unknown_account_returns_404()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{Guid.NewGuid()}/register/summary?from=2026-01-01&to=2026-12-31&bucket=Month",
                UriKind.Relative
            )
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("to=2026-12-31&bucket=Month")]
    [Arguments("from=2026-01-01&bucket=Month")]
    [Arguments("from=2026-01-01&to=2026-12-31")]
    public async Task GetSummary_with_missing_required_parameter_returns_400(string query)
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(
            client,
            $"Sum-Req-{Guid.NewGuid():N}",
            "Asset",
            "EUR"
        );

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{account.Id}/register/summary?{query}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetSummary_with_to_before_from_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(
            client,
            $"Sum-Rng-{Guid.NewGuid():N}",
            "Asset",
            "EUR"
        );

        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{account.Id}/register/summary?from=2026-06-04&to=2026-06-01&bucket=Day",
                UriKind.Relative
            )
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetSummary_with_range_too_long_for_bucket_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(
            client,
            $"Sum-Cap-{Guid.NewGuid():N}",
            "Asset",
            "EUR"
        );

        // Two years of daily buckets is past the 400-bucket cap.
        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{account.Id}/register/summary?from=2024-01-01&to=2026-01-01&bucket=Day",
                UriKind.Relative
            )
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetSummary_for_leaf_account_yields_single_self_segment()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Sum-Leaf-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Sum-Leaf-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 2, 10),
            [
                new CreateJournalLineRequestDto(checking.Id, 250_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -250_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(
            client,
            checking.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31),
            "Month"
        );

        await Assert.That(summary.CurrencyCode).IsEqualTo(currency);
        await Assert.That(summary.Segments.Count).IsEqualTo(1);
        await Assert.That(summary.Segments[0].AccountId).IsEqualTo(checking.Id);

        // Buckets cover the whole range gaplessly; only February carries a value.
        await Assert.That(summary.Buckets.Count).IsEqualTo(3);
        await Assert.That(summary.Buckets[0].Start).IsEqualTo(new DateOnly(2026, 1, 1));
        await Assert.That(summary.Buckets[0].Values.Count).IsEqualTo(0);
        await Assert.That(summary.Buckets[1].Start).IsEqualTo(new DateOnly(2026, 2, 1));
        await Assert.That(summary.Buckets[1].Values.Count).IsEqualTo(1);
        await Assert.That(summary.Buckets[1].Values[0].Amount).IsEqualTo(250_000L);
        await Assert.That(summary.Buckets[2].Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetSummary_for_credit_normal_account_flips_sign()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Sum-CN-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Sum-CN-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 2, 10),
            [
                new CreateJournalLineRequestDto(checking.Id, 250_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -250_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(
            client,
            salary.Id,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            "Month"
        );

        // The raw line is a credit (-250k); income reads positive per the normal balance.
        await Assert.That(summary.Buckets.Count).IsEqualTo(1);
        await Assert.That(summary.Buckets[0].Values.Count).IsEqualTo(1);
        await Assert.That(summary.Buckets[0].Values[0].Amount).IsEqualTo(250_000L);
    }

    [Test]
    public async Task GetSummary_for_parent_rolls_grandchildren_into_direct_children()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var income = await CreateAccountAsync(
            client,
            $"Sum-Tree-Income-{Guid.NewGuid():N}",
            "Income",
            currency
        );
        var parent = await CreateAccountAsync(
            client,
            $"Sum-Tree-Parent-{Guid.NewGuid():N}",
            "Asset",
            currency,
            isPostable: false
        );
        var childA = await CreateAccountAsync(
            client,
            $"Sum-Tree-A-{Guid.NewGuid():N}",
            "Asset",
            currency,
            parentAccountId: parent.Id
        );
        var childB = await CreateAccountAsync(
            client,
            $"Sum-Tree-B-{Guid.NewGuid():N}",
            "Asset",
            currency,
            isPostable: false,
            parentAccountId: parent.Id
        );
        var grandchild = await CreateAccountAsync(
            client,
            $"Sum-Tree-B1-{Guid.NewGuid():N}",
            "Asset",
            currency,
            parentAccountId: childB.Id
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 5),
            [
                new CreateJournalLineRequestDto(childA.Id, 10_000L, null),
                new CreateJournalLineRequestDto(income.Id, -10_000L, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 20),
            [
                new CreateJournalLineRequestDto(grandchild.Id, 7_000L, null),
                new CreateJournalLineRequestDto(income.Id, -7_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(
            client,
            parent.Id,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31),
            "Month"
        );

        // Two segments: the direct children. The grandchild's activity surfaces under childB.
        await Assert.That(summary.Segments.Count).IsEqualTo(2);
        var segmentIds = summary.Segments.Select(s => s.AccountId).ToList();
        await Assert.That(segmentIds).Contains(childA.Id);
        await Assert.That(segmentIds).Contains(childB.Id);

        await Assert.That(summary.Buckets.Count).IsEqualTo(1);
        var values = summary.Buckets[0].Values.ToDictionary(v => v.AccountId, v => v.Amount);
        await Assert.That(values[childA.Id]).IsEqualTo(10_000L);
        await Assert.That(values[childB.Id]).IsEqualTo(7_000L);
    }

    [Test]
    public async Task GetSummary_keeps_both_legs_of_an_intra_subtree_transfer()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var parent = await CreateAccountAsync(
            client,
            $"Sum-Xfer-Parent-{Guid.NewGuid():N}",
            "Asset",
            currency,
            isPostable: false
        );
        var childA = await CreateAccountAsync(
            client,
            $"Sum-Xfer-A-{Guid.NewGuid():N}",
            "Asset",
            currency,
            parentAccountId: parent.Id
        );
        var childB = await CreateAccountAsync(
            client,
            $"Sum-Xfer-B-{Guid.NewGuid():N}",
            "Asset",
            currency,
            parentAccountId: parent.Id
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 5, 5),
            [
                new CreateJournalLineRequestDto(childA.Id, 5_000L, null),
                new CreateJournalLineRequestDto(childB.Id, -5_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(
            client,
            parent.Id,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31),
            "Month"
        );

        // No elimination (ADR-0019): both legs show, netting to zero across the stack.
        var values = summary.Buckets[0].Values.ToDictionary(v => v.AccountId, v => v.Amount);
        await Assert.That(values[childA.Id]).IsEqualTo(5_000L);
        await Assert.That(values[childB.Id]).IsEqualTo(-5_000L);
    }

    [Test]
    public async Task GetSummary_week_buckets_start_on_monday()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);
        var checking = await CreateAccountAsync(
            client,
            $"Sum-Wk-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var salary = await CreateAccountAsync(
            client,
            $"Sum-Wk-Salary-{Guid.NewGuid():N}",
            "Income",
            currency
        );

        // 2026-06-03 is a Wednesday; its ISO week starts Monday 2026-06-01.
        await PostJournalEntryAsync(
            client,
            new DateOnly(2026, 6, 3),
            [
                new CreateJournalLineRequestDto(checking.Id, 1_000L, null),
                new CreateJournalLineRequestDto(salary.Id, -1_000L, null),
            ]
        );

        var summary = await GetSummaryAsync(
            client,
            checking.Id,
            new DateOnly(2026, 6, 3),
            new DateOnly(2026, 6, 9),
            "Week"
        );

        await Assert.That(summary.Buckets.Count).IsEqualTo(2);
        await Assert.That(summary.Buckets[0].Start).IsEqualTo(new DateOnly(2026, 6, 1));
        await Assert.That(summary.Buckets[0].Values.Count).IsEqualTo(1);
        await Assert.That(summary.Buckets[1].Start).IsEqualTo(new DateOnly(2026, 6, 8));
        await Assert.That(summary.Buckets[1].Values.Count).IsEqualTo(0);
    }

    private static async Task<RegisterSummaryDto> GetSummaryAsync(
        HttpClient client,
        Guid accountId,
        DateOnly from,
        DateOnly to,
        string bucket
    )
    {
        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{accountId}/register/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&bucket={bucket}",
                UriKind.Relative
            )
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<RegisterSummaryDto>();
        return dto!;
    }

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
        string currencyCode,
        bool isPostable = true,
        Guid? parentAccountId = null
    )
    {
        var req = new CreateAccountRequestDto(name, accountType, currencyCode)
        {
            IsPostable = isPostable,
            ParentAccountId = parentAccountId,
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

    // Same isolation trick as the register tests — keep each test's accounts and journal lines on
    // a freshly-minted CurrencyCode so cross-class iteration doesn't bleed sums.
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

internal sealed record RegisterSummaryDto(
    string Bucket,
    DateOnly From,
    DateOnly To,
    string CurrencyCode,
    IReadOnlyList<RegisterSummarySegmentDto> Segments,
    IReadOnlyList<RegisterSummaryBucketDto> Buckets
);

internal sealed record RegisterSummarySegmentDto(Guid AccountId, string AccountName);

internal sealed record RegisterSummaryBucketDto(
    DateOnly Start,
    IReadOnlyList<RegisterSummaryValueDto> Values
);

internal sealed record RegisterSummaryValueDto(Guid AccountId, long Amount);
