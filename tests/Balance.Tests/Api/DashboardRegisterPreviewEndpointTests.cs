using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class DashboardRegisterPreviewEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task GetRegisterPreviews_returns_the_five_newest_rows_per_account()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var checking = await CreateAccountAsync(
            client,
            $"RP-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var equity = await CreateAccountAsync(
            client,
            $"RP-Equity-{Guid.NewGuid():N}",
            "Equity",
            currency
        );

        // Seven entries on distinct dates; only the five newest may come back.
        for (var day = 1; day <= 7; day++)
        {
            await PostJournalEntryAsync(
                client,
                new DateOnly(2024, 3, day),
                [
                    new CreateJournalLineRequestDto(checking.Id, 1_000L * day, null),
                    new CreateJournalLineRequestDto(equity.Id, -1_000L * day, null),
                ]
            );
        }

        var previews = await GetRegisterPreviewsAsync(client);
        await Assert.That(previews.RowsPerAccount).IsEqualTo(5);

        var rows = previews.Accounts.Single(a => a.AccountId == checking.Id).Rows;
        await Assert.That(rows.Count).IsEqualTo(5);
        await Assert
            .That(rows.Select(r => r.Date).ToList())
            .IsEquivalentTo([
                new DateOnly(2024, 3, 7),
                new DateOnly(2024, 3, 6),
                new DateOnly(2024, 3, 5),
                new DateOnly(2024, 3, 4),
                new DateOnly(2024, 3, 3),
            ]);
    }

    [Test]
    public async Task GetRegisterPreviews_applies_the_normal_balance_sign_convention()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var asset = await CreateAccountAsync(
            client,
            $"RP-Asset-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var liability = await CreateAccountAsync(
            client,
            $"RP-Liability-{Guid.NewGuid():N}",
            "Liability",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 5, 1),
            [
                new CreateJournalLineRequestDto(asset.Id, 250_000L, null),
                new CreateJournalLineRequestDto(liability.Id, -250_000L, null),
            ]
        );

        var previews = await GetRegisterPreviewsAsync(client);

        // Debit-normal keeps the raw sign; credit-normal flips it (ADR-0011), matching the
        // Register the dashboard rows preview.
        var assetRow = previews.Accounts.Single(a => a.AccountId == asset.Id).Rows.Single();
        await Assert.That(assetRow.Amount!.Amount).IsEqualTo(250_000L);
        await Assert.That(assetRow.Amount.CurrencyCode).IsEqualTo(currency);

        var liabilityRow = previews.Accounts.Single(a => a.AccountId == liability.Id).Rows.Single();
        await Assert.That(liabilityRow.Amount!.Amount).IsEqualTo(250_000L);
    }

    [Test]
    public async Task GetRegisterPreviews_omits_branch_accounts_and_accounts_without_activity()
    {
        using var client = Factory.CreateClient();
        var currency = await CreateIsolatedCurrencyAsync(client);

        var branch = await CreateAccountAsync(
            client,
            $"RP-Branch-{Guid.NewGuid():N}",
            "Expense",
            currency,
            isPostable: false
        );
        var leaf = await CreateAccountAsync(
            client,
            $"RP-Leaf-{Guid.NewGuid():N}",
            "Expense",
            currency,
            parentAccountId: branch.Id
        );
        var idle = await CreateAccountAsync(
            client,
            $"RP-Idle-{Guid.NewGuid():N}",
            "Asset",
            currency
        );
        var checking = await CreateAccountAsync(
            client,
            $"RP-Checking-{Guid.NewGuid():N}",
            "Asset",
            currency
        );

        await PostJournalEntryAsync(
            client,
            new DateOnly(2024, 5, 1),
            [
                new CreateJournalLineRequestDto(leaf.Id, 4_500L, null),
                new CreateJournalLineRequestDto(checking.Id, -4_500L, null),
            ]
        );

        var previews = await GetRegisterPreviewsAsync(client);
        var accountIds = previews.Accounts.Select(a => a.AccountId).ToList();

        await Assert.That(accountIds).Contains(leaf.Id);
        await Assert.That(accountIds).DoesNotContain(branch.Id);
        await Assert.That(accountIds).DoesNotContain(idle.Id);
    }

    private static async Task<DashboardRegisterPreviewDto> GetRegisterPreviewsAsync(
        HttpClient client
    )
    {
        using var response = await client.GetAsync(
            new Uri("/api/dashboard/register-previews", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var previews = await response.Content.ReadFromJsonAsync<DashboardRegisterPreviewDto>();
        return previews!;
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
        string currencyCode = "EUR",
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
}

internal sealed record DashboardRegisterPreviewDto(
    int RowsPerAccount,
    IReadOnlyList<AccountRegisterPreviewDto> Accounts
);

internal sealed record AccountRegisterPreviewDto(
    Guid AccountId,
    IReadOnlyList<RegisterPreviewRowDto> Rows
);

internal sealed record RegisterPreviewRowDto(
    Guid JournalEntryId,
    Guid JournalLineId,
    DateOnly Date,
    string? EntryDescription,
    string? LineDescription,
    string? CounterpartyName,
    MoneyDto? Amount
);
