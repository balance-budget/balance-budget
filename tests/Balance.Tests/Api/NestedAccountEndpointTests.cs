using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

/// <summary>
/// Covers the chart-of-accounts tree behaviour from ADR-0019: parent validation, postability
/// conversion gating, cycle prevention, rolled-up balances, the aggregated parent register, and
/// the parent-delete RESTRICT.
/// </summary>
internal sealed class NestedAccountEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task CreateAccount_under_non_postable_parent_of_same_type_succeeds()
    {
        using var client = Factory.CreateClient();
        var parent = await CreateAsync(client, "Food", "Expense", isPostable: false);

        var child = new CreateAccountRequestDto($"Groceries-{Guid.NewGuid():N}", "Expense", "EUR")
        {
            Code = UniqueCode(),
            ParentAccountId = parent.Id,
        };
        using var response = await client.PostAsJsonAsync(AccountsUri, child);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AccountDto>();
        await Assert.That(created!.ParentAccountId).IsEqualTo(parent.Id);
    }

    [Test]
    public async Task CreateAccount_under_postable_parent_returns_422()
    {
        using var client = Factory.CreateClient();
        var parent = await CreateAsync(client, "Cash", "Asset", isPostable: true);

        var child = new CreateAccountRequestDto($"Wallet-{Guid.NewGuid():N}", "Asset", "EUR")
        {
            Code = UniqueCode(),
            ParentAccountId = parent.Id,
        };
        using var response = await client.PostAsJsonAsync(AccountsUri, child);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateAccount_under_parent_of_different_type_returns_422()
    {
        using var client = Factory.CreateClient();
        var parent = await CreateAsync(client, "Food", "Expense", isPostable: false);

        var child = new CreateAccountRequestDto($"Salary-{Guid.NewGuid():N}", "Income", "EUR")
        {
            Code = UniqueCode(),
            ParentAccountId = parent.Id,
        };
        using var response = await client.PostAsJsonAsync(AccountsUri, child);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ParentBalance_rolls_up_descendant_leaves()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAsync(client, "Food", "Expense", isPostable: false);
        var groceries = await CreateChildAsync(client, "Groceries", "Expense", food.Id);
        var dining = await CreateChildAsync(client, "Dining", "Expense", food.Id);
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");

        // €200 groceries + €150 dining, both funded from checking.
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 20_000, null),
                new CreateJournalLineRequestDto(checking.Id, -20_000, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(dining.Id, 15_000, null),
                new CreateJournalLineRequestDto(checking.Id, -15_000, null),
            ]
        );

        using var balanceResponse = await client.GetAsync(
            new Uri($"/api/accounts/{food.Id}/balance", UriKind.Relative)
        );
        await Assert.That(balanceResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var balance = await balanceResponse.Content.ReadFromJsonAsync<MoneyDto>();
        await Assert.That(balance!.Amount).IsEqualTo(35_000L);

        // The list endpoint must roll up identically.
        using var listResponse = await client.GetAsync(AccountsUri);
        var accounts = await listResponse.Content.ReadPagedItemsAsync<AccountDto>();
        await Assert.That(accounts.Single(a => a.Id == food.Id).Balance!.Amount).IsEqualTo(35_000L);
    }

    [Test]
    public async Task ParentRegister_aggregates_descendant_lines()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAsync(client, "Food", "Expense", isPostable: false);
        var groceries = await CreateChildAsync(client, "Groceries", "Expense", food.Id);
        var dining = await CreateChildAsync(client, "Dining", "Expense", food.Id);
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");

        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 20_000, null),
                new CreateJournalLineRequestDto(checking.Id, -20_000, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(dining.Id, 15_000, null),
                new CreateJournalLineRequestDto(checking.Id, -15_000, null),
            ]
        );

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{food.Id}/register", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var rows = await response.Content.ReadPagedItemsAsync<RegisterRowDto>();
        // One row per descendant leaf line — both the groceries and the dining postings.
        await Assert.That(rows.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ParentRegister_rows_carry_the_descendant_posted_account()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAsync(client, "Food", "Expense", isPostable: false);
        var groceries = await CreateChildAsync(client, "Groceries", "Expense", food.Id);
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");

        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 20_000, null),
                new CreateJournalLineRequestDto(checking.Id, -20_000, null),
            ]
        );

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{food.Id}/register", UriKind.Relative)
        );
        var rows = await response.Content.ReadPagedItemsAsync<RegisterRowDto>();

        // The row landed on the Groceries leaf, not on the viewed Food parent.
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].AccountId).IsEqualTo(groceries.Id);
        await Assert.That(rows[0].AccountName).IsEqualTo(groceries.Name);
    }

    [Test]
    public async Task ParentRegister_posted_account_filter_narrows_to_one_descendant()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAsync(client, "Food", "Expense", isPostable: false);
        var groceries = await CreateChildAsync(client, "Groceries", "Expense", food.Id);
        var dining = await CreateChildAsync(client, "Dining", "Expense", food.Id);
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");

        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 20_000, null),
                new CreateJournalLineRequestDto(checking.Id, -20_000, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(dining.Id, 15_000, null),
                new CreateJournalLineRequestDto(checking.Id, -15_000, null),
            ]
        );

        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{food.Id}/register?postedAccountId={dining.Id}",
                UriKind.Relative
            )
        );
        var rows = await response.Content.ReadPagedItemsAsync<RegisterRowDto>();

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].AccountId).IsEqualTo(dining.Id);
    }

    [Test]
    public async Task ParentRegister_posted_account_filter_outside_subtree_matches_nothing()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAsync(client, "Food", "Expense", isPostable: false);
        var groceries = await CreateChildAsync(client, "Groceries", "Expense", food.Id);
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");

        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 20_000, null),
                new CreateJournalLineRequestDto(checking.Id, -20_000, null),
            ]
        );

        // Checking exists but lies outside Food's subtree — the intersection is empty.
        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{food.Id}/register?postedAccountId={checking.Id}",
                UriKind.Relative
            )
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var rows = await response.Content.ReadPagedItemsAsync<RegisterRowDto>();
        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Register_counter_account_filter_matches_whole_subtree()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAsync(client, "Food", "Expense", isPostable: false);
        var groceries = await CreateChildAsync(client, "Groceries", "Expense", food.Id);
        var rent = await CreateAsync(client, $"Rent-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");

        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(groceries.Id, 20_000, null),
                new CreateJournalLineRequestDto(checking.Id, -20_000, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(rent.Id, 120_000, null),
                new CreateJournalLineRequestDto(checking.Id, -120_000, null),
            ]
        );

        // Filtering Checking's register on the non-postable Food matches the Groceries leg.
        using var response = await client.GetAsync(
            new Uri(
                $"/api/accounts/{checking.Id}/register?counterAccountId={food.Id}",
                UriKind.Relative
            )
        );
        var rows = await response.Content.ReadPagedItemsAsync<RegisterRowDto>();

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].AccountId).IsEqualTo(checking.Id);
    }

    [Test]
    public async Task ConvertAccount_with_lines_to_non_postable_returns_422()
    {
        using var client = Factory.CreateClient();
        var checking = await CreateAsync(client, $"Checking-{Guid.NewGuid():N}", "Asset");
        var salary = await CreateAsync(client, $"Salary-{Guid.NewGuid():N}", "Income");
        await PostJournalEntryAsync(
            client,
            [
                new CreateJournalLineRequestDto(checking.Id, 50_000, null),
                new CreateJournalLineRequestDto(salary.Id, -50_000, null),
            ]
        );

        // Try to flip the account that now carries lines into a non-postable roll-up.
        using var response = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/accounts/{checking.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/isPostable", false)]
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task Reparent_under_own_descendant_returns_422()
    {
        using var client = Factory.CreateClient();
        var top = await CreateAsync(client, "Top", "Expense", isPostable: false);
        var mid = await CreateChildAsync(client, "Mid", "Expense", top.Id, isPostable: false);

        // Moving Top under Mid (its own child) would create a cycle.
        using var response = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/accounts/{top.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/parentAccountId", mid.Id.ToString())]
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task DeleteAccount_with_children_returns_conflict()
    {
        using var client = Factory.CreateClient();
        var parent = await CreateAsync(client, "Food", "Expense", isPostable: false);
        await CreateChildAsync(client, "Groceries", "Expense", parent.Id);

        using var response = await client.DeleteAsync(
            new Uri($"/api/accounts/{parent.Id}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    private static readonly Uri AccountsUri = new("/api/accounts", UriKind.Relative);

    private static string UniqueCode() => $"C{Guid.NewGuid():N}"[..14];

    private static async Task<AccountDto> CreateAsync(
        HttpClient client,
        string name,
        string accountType,
        bool isPostable = true
    )
    {
        var req = new CreateAccountRequestDto($"{name}", accountType, "EUR")
        {
            Code = UniqueCode(),
            IsPostable = isPostable,
        };
        using var response = await client.PostAsJsonAsync(AccountsUri, req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    private static async Task<AccountDto> CreateChildAsync(
        HttpClient client,
        string name,
        string accountType,
        Guid parentId,
        bool isPostable = true
    )
    {
        var req = new CreateAccountRequestDto($"{name}-{Guid.NewGuid():N}", accountType, "EUR")
        {
            Code = UniqueCode(),
            IsPostable = isPostable,
            ParentAccountId = parentId,
        };
        using var response = await client.PostAsJsonAsync(AccountsUri, req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    private static async Task PostJournalEntryAsync(
        HttpClient client,
        IReadOnlyList<CreateJournalLineRequestDto> lines
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
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

    private sealed record RegisterRowDto(
        Guid JournalEntryId,
        Guid JournalLineId,
        Guid AccountId,
        string AccountName
    );
}
