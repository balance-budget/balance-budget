using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class JournalEntryEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListJournalEntries_returns_ok()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/journal-entries", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<JournalEntryDto>>();
        await Assert.That(list).IsNotNull();
    }

    [Test]
    public async Task CreateJournalEntry_balanced_two_line_round_trips()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, "Groceries Expense", "Expense");
        var checking = await CreateAccountAsync(client, "Checking BTX-JE-1", "Asset");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: "Groceries at AH",
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 4000, "Albert Heijn"),
                new CreateJournalLineRequestDto(checking.Id, -4000, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Lines).Count().IsEqualTo(2);
        await Assert.That(created.Lines.Sum(l => l.Amount)).IsEqualTo(0L);

        using var getResponse = await client.GetAsync(
            new Uri($"/journal-entries/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(fetched!.Lines).Count().IsEqualTo(2);
        await Assert.That(fetched.Date).IsEqualTo(new DateOnly(2026, 5, 17));
        await Assert.That(fetched.Description).IsEqualTo("Groceries at AH");
        await Assert.That(fetched.Lines.All(l => l.ReconciliationStatus == "Uncleared")).IsTrue();
    }

    [Test]
    public async Task CreateJournalEntry_unbalanced_returns_422()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, "Groceries-Unbalanced", "Expense");
        var checking = await CreateAccountAsync(client, "Checking-Unbalanced", "Asset");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 4000, null),
                new CreateJournalLineRequestDto(checking.Id, -3000, null),
            ]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateJournalEntry_single_line_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Single-Line-Acc", "Asset");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines: [new CreateJournalLineRequestDto(account.Id, 100, null)]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateJournalEntry_currency_mismatch_returns_422()
    {
        using var client = Factory.CreateClient();
        var eurAccount = await CreateAccountAsync(client, "Currency-EUR", "Asset", "EUR");
        var usdAccount = await CreateAccountAsync(client, "Currency-USD", "Asset", "USD");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(eurAccount.Id, 4000, null),
                new CreateJournalLineRequestDto(usdAccount.Id, -4000, null),
            ]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task GetJournalEntry_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/journal-entries/{Guid.NewGuid()}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteJournalEntry_cascades_lines()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "Delete-A", "Expense");
        var b = await CreateAccountAsync(client, "Delete-B", "Asset");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 100, null),
                new CreateJournalLineRequestDto(b.Id, -100, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/journal-entries/{created!.Id}", UriKind.Relative)
        );
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/journal-entries/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateJournalEntry_replaces_lines()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "Update-A", "Expense");
        var b = await CreateAccountAsync(client, "Update-B", "Asset");

        var create = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 1),
            Description: "before",
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 500, null),
                new CreateJournalLineRequestDto(b.Id, -500, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        var update = new UpdateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 2),
            Description: "after",
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 1500, null),
                new CreateJournalLineRequestDto(b.Id, -1500, null),
            ]
        );
        using var patchResponse = await client.PatchAsJsonAsync(
            new Uri($"/journal-entries/{created!.Id}", UriKind.Relative),
            update
        );
        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(updated!.Description).IsEqualTo("after");
        await Assert.That(updated.Date).IsEqualTo(new DateOnly(2026, 5, 2));
        await Assert.That(updated.Lines.Sum(l => Math.Abs(l.Amount))).IsEqualTo(3000L);
    }

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType = "Asset",
        string currencyCode = "EUR"
    )
    {
        var req = new CreateAccountRequestDto(name, accountType, currencyCode);
        using var response = await client.PostAsJsonAsync(
            new Uri("/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }
}

internal sealed record JournalEntryDto(
    Guid Id,
    DateOnly Date,
    string? Description,
    Guid? BankTransactionId,
    Guid? CounterpartyId,
    IReadOnlyList<JournalLineDto> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

internal sealed record JournalLineDto(
    Guid Id,
    Guid AccountId,
    long Amount,
    string ReconciliationStatus,
    string? Description
);

internal sealed record CreateJournalEntryRequestDto(
    DateOnly Date,
    string? Description,
    Guid? BankTransactionId,
    Guid? CounterpartyId,
    IReadOnlyList<CreateJournalLineRequestDto> Lines
);

internal sealed record CreateJournalLineRequestDto(Guid AccountId, long Amount, string? Description);

internal sealed record UpdateJournalEntryRequestDto(
    DateOnly? Date,
    string? Description,
    Guid? BankTransactionId,
    Guid? CounterpartyId,
    IReadOnlyList<CreateJournalLineRequestDto>? Lines
);
