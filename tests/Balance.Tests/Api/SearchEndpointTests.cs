using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class SearchEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task Search_returns_grouped_hits_across_entity_types()
    {
        using var client = Factory.CreateClient();

        var name = $"Albert-{Guid.NewGuid():N}";
        // Account whose name matches.
        var account = await PostAsync<CreateAccountRequestDto, AccountDto>(
            client,
            "/api/accounts",
            new CreateAccountRequestDto(name, "Expense", "EUR")
        );
        // Counterparty whose name matches.
        await PostAsync<CreateCounterpartyRequestDto, CounterpartyDto>(
            client,
            "/api/counterparties",
            new CreateCounterpartyRequestDto($"{name} B.V.")
        );
        // JournalEntry whose description matches.
        var checking = await PostAsync<CreateAccountRequestDto, AccountDto>(
            client,
            "/api/accounts",
            new CreateAccountRequestDto($"Checking-{Guid.NewGuid():N}", "Asset", "EUR")
        );
        await PostAsync<CreateJournalEntryRequestDto, object>(
            client,
            "/api/journal-entries",
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 1, 1),
                Description: $"{name} weekly shop",
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineRequestDto(account.Id, 1000, null),
                    new CreateJournalLineRequestDto(checking.Id, -1000, null),
                ]
            )
        );

        using var response = await client.GetAsync(
            new Uri($"/api/search?q={name[..6]}", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var output = await response.Content.ReadFromJsonAsync<SearchOutputDto>();
        await Assert.That(output).IsNotNull();
        await Assert.That(output!.Accounts.TotalCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(output.Counterparties.TotalCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(output.JournalEntries.TotalCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Search_query_below_min_length_returns_400()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/search?q=a", UriKind.Relative));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Search_pages_section_matches_label_case_insensitive()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/api/search?q=journal", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var output = await response.Content.ReadFromJsonAsync<SearchOutputDto>();
        await Assert.That(output!.Pages.Items.Select(p => p.Label)).Contains("Journal");
    }

    private static async Task<TOut> PostAsync<TIn, TOut>(HttpClient client, string url, TIn body)
        where TOut : notnull
    {
        using var response = await client.PostAsJsonAsync(new Uri(url, UriKind.Relative), body);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TOut>();
        return dto ?? throw new InvalidOperationException($"POST {url} returned no body.");
    }
}

internal sealed record SearchOutputDto(
    SearchSectionDto<SearchAccountHitDto> Accounts,
    SearchSectionDto<SearchCounterpartyHitDto> Counterparties,
    SearchSectionDto<SearchBankAccountHitDto> BankAccounts,
    SearchSectionDto<SearchJournalEntryHitDto> JournalEntries,
    SearchSectionDto<SearchPageHitDto> Pages
);

internal sealed record SearchSectionDto<T>(IReadOnlyList<T> Items, int TotalCount);

internal sealed record SearchAccountHitDto(Guid Id, string Name, string AccountType);

internal sealed record SearchCounterpartyHitDto(Guid Id, string Name);

internal sealed record SearchBankAccountHitDto(
    Guid Id,
    string Type,
    string? Iban,
    string? AccountNumber,
    string? CardIdentifier,
    string? BankName,
    string? AccountHolderName
);

internal sealed record SearchJournalEntryHitDto(Guid Id, DateOnly Date, string? Description);

internal sealed record SearchPageHitDto(string Label, string Route);
