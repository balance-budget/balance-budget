using System.Net;
using System.Net.Http.Json;
using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task UpdateJournalEntry_patches_date_and_description()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "Patch-Entry-A", "Expense");
        var b = await CreateAccountAsync(client, "Patch-Entry-B", "Asset");

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

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/journal-entries/{created!.Id}", UriKind.Relative),
            [
                JsonPatchHelpers.Replace("/date", "2026-05-02"),
                JsonPatchHelpers.Replace("/description", "after"),
            ]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(updated!.Description).IsEqualTo("after");
        await Assert.That(updated.Date).IsEqualTo(new DateOnly(2026, 5, 2));
        await Assert.That(updated.Lines.Sum(l => Math.Abs(l.Amount))).IsEqualTo(1000L);
    }

    [Test]
    public async Task UpdateJournalEntry_patches_line_description_only()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "Line-Desc-A", "Expense");
        var b = await CreateAccountAsync(client, "Line-Desc-B", "Asset");

        var create = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 5),
            Description: "entry",
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 700, "original"),
                new CreateJournalLineRequestDto(b.Id, -700, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var firstLine = created!.Lines.Single(l => l.Amount == 700);

        var key = firstLine.Id.ToString("D");
        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/journal-entries/{created.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace($"/lines/{key}/description", "edited")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var editedLine = updated!.Lines.Single(l => l.Id == firstLine.Id);
        await Assert.That(editedLine.Description).IsEqualTo("edited");
        // Other line unchanged
        var otherLine = updated.Lines.Single(l => l.Id != firstLine.Id);
        await Assert.That(otherLine.Description).IsNull();
        // Amounts preserved
        await Assert.That(updated.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
        await Assert.That(updated.Lines.Single(l => l.Id == firstLine.Id).Amount).IsEqualTo(700L);
    }

    [Test]
    public async Task UpdateJournalEntry_removing_a_line_returns_422()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "NoRemove-A", "Expense");
        var b = await CreateAccountAsync(client, "NoRemove-B", "Asset");

        var create = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 6),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 200, null),
                new CreateJournalLineRequestDto(b.Id, -200, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var line = created!.Lines[0];
        var key = line.Id.ToString("D");

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/journal-entries/{created.Id}", UriKind.Relative),
            [JsonPatchHelpers.Remove($"/lines/{key}")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task UpdateJournalEntry_adding_a_line_returns_422()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "NoAdd-A", "Expense");
        var b = await CreateAccountAsync(client, "NoAdd-B", "Asset");

        var create = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 7),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 300, null),
                new CreateJournalLineRequestDto(b.Id, -300, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        var fakeKey = Guid.NewGuid().ToString("D");
        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/journal-entries/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Add($"/lines/{fakeKey}", new { description = "snuck in" })]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task UpdateJournalEntry_preserves_cleared_reconciliation_status_after_description_patch()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "Reconcile-A", "Expense");
        var b = await CreateAccountAsync(client, "Reconcile-B", "Asset");

        var create = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 8),
            Description: "groceries",
            BankTransactionId: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 900, "Albert Heijn"),
                new CreateJournalLineRequestDto(b.Id, -900, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var firstLine = created!.Lines.Single(l => l.Amount == 900);

        // Simulate a reconciliation having marked the line as Cleared by writing directly
        // through the test fixture's service scope. The PATCH below must not clobber this.
        await MarkLineClearedAsync(firstLine.Id);

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/journal-entries/{created.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/description", "Albert Heijn, NL")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var preserved = updated!.Lines.Single(l => l.Id == firstLine.Id);
        await Assert.That(preserved.ReconciliationStatus).IsEqualTo("Cleared");
        await Assert.That(updated.Description).IsEqualTo("Albert Heijn, NL");
    }

    [Test]
    public async Task UpdateJournalEntry_with_invalid_path_returns_400()
    {
        using var client = Factory.CreateClient();
        var a = await CreateAccountAsync(client, "BadPath-A", "Expense");
        var b = await CreateAccountAsync(client, "BadPath-B", "Asset");

        var create = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 9),
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
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/journal-entries/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/bogusField", "x")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private async Task MarkLineClearedAsync(Guid lineId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var line = await dbContext.JournalLines.SingleAsync(l => l.Id == new JournalLineId(lineId));
        line.ReconciliationStatus = ReconciliationStatus.Cleared;
        await dbContext.SaveChangesAsync();
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

internal sealed record CreateJournalLineRequestDto(
    Guid AccountId,
    long Amount,
    string? Description
);
