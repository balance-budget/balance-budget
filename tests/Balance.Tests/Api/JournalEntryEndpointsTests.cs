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

        using var response = await client.GetAsync(
            new Uri("/api/journal-entries", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadPagedItemsAsync<JournalEntryDto>();
        await Assert.That(list).IsNotNull();
    }

    [Test]
    public async Task ListJournalEntries_q_filters_by_description_case_insensitive()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(
            client,
            $"Groceries-Search-{Guid.NewGuid():N}",
            "Expense"
        );
        var checking = await CreateAccountAsync(
            client,
            $"Checking-Search-{Guid.NewGuid():N}",
            "Asset"
        );

        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 1, 1),
                Description: "Albert Heijn weekly shop",
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineRequestDto(groceries.Id, 1000, null),
                    new CreateJournalLineRequestDto(checking.Id, -1000, null),
                ]
            )
        );
        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 1, 2),
                Description: "Vattenfall energy",
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineRequestDto(groceries.Id, 2000, null),
                    new CreateJournalLineRequestDto(checking.Id, -2000, null),
                ]
            )
        );

        using var filtered = await client.GetAsync(
            new Uri("/api/journal-entries?q=albert", UriKind.Relative)
        );
        var rows = await filtered.Content.ReadPagedItemsAsync<JournalEntryDto>();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Description).IsEqualTo("Albert Heijn weekly shop");
    }

    [Test]
    public async Task ListJournalEntries_q_matches_linked_counterparty_name()
    {
        using var client = Factory.CreateClient();
        var expense = await CreateAccountAsync(client, $"E-CPQ-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"C-CPQ-{Guid.NewGuid():N}", "Asset");
        // A token that lives only in one counterparty's name and in no description, so a
        // match can only come from counterparty-name matching.
        var token = Guid.NewGuid().ToString("N")[..10];
        var named = await PostCounterpartyAsync(client, $"Supermarket {token}");
        var other = await PostCounterpartyAsync(client, $"Utility-{Guid.NewGuid():N}");

        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 2, 1),
                Description: "Weekly shop",
                CounterpartyId: named.Id,
                Lines:
                [
                    new CreateJournalLineRequestDto(expense.Id, 1000, null),
                    new CreateJournalLineRequestDto(checking.Id, -1000, null),
                ]
            )
        );
        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 2, 2),
                Description: "Energy bill",
                CounterpartyId: other.Id,
                Lines:
                [
                    new CreateJournalLineRequestDto(expense.Id, 2000, null),
                    new CreateJournalLineRequestDto(checking.Id, -2000, null),
                ]
            )
        );

        using var filtered = await client.GetAsync(
            new Uri($"/api/journal-entries?q={token.ToUpperInvariant()}", UriKind.Relative)
        );
        var rows = await filtered.Content.ReadPagedItemsAsync<JournalEntryDto>();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Description).IsEqualTo("Weekly shop");
    }

    [Test]
    public async Task ListJournalEntries_counterpartyId_filters_to_that_counterparty()
    {
        using var client = Factory.CreateClient();
        var expense = await CreateAccountAsync(client, $"E-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"C-{Guid.NewGuid():N}", "Asset");
        var alice = await PostCounterpartyAsync(client, $"Alice-{Guid.NewGuid():N}");
        var bob = await PostCounterpartyAsync(client, $"Bob-{Guid.NewGuid():N}");

        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 1, 1),
                Description: "From Alice",
                CounterpartyId: alice.Id,
                Lines:
                [
                    new CreateJournalLineRequestDto(expense.Id, 1000, null),
                    new CreateJournalLineRequestDto(checking.Id, -1000, null),
                ]
            )
        );
        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 1, 2),
                Description: "From Bob",
                CounterpartyId: bob.Id,
                Lines:
                [
                    new CreateJournalLineRequestDto(expense.Id, 2000, null),
                    new CreateJournalLineRequestDto(checking.Id, -2000, null),
                ]
            )
        );

        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries?counterpartyId={alice.Id}", UriKind.Relative)
        );
        var rows = await response.Content.ReadPagedItemsAsync<JournalEntryDto>();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Description).IsEqualTo("From Alice");
    }

    [Test]
    public async Task ListJournalEntries_accountId_filters_to_entries_touching_the_account()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, $"G-{Guid.NewGuid():N}", "Expense");
        var rent = await CreateAccountAsync(client, $"R-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"C-{Guid.NewGuid():N}", "Asset");

        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 2, 1),
                Description: "Groceries entry",
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineRequestDto(groceries.Id, 1000, null),
                    new CreateJournalLineRequestDto(checking.Id, -1000, null),
                ]
            )
        );
        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 2, 2),
                Description: "Rent entry",
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineRequestDto(rent.Id, 2000, null),
                    new CreateJournalLineRequestDto(checking.Id, -2000, null),
                ]
            )
        );

        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries?accountId={groceries.Id}", UriKind.Relative)
        );
        var rows = await response.Content.ReadPagedItemsAsync<JournalEntryDto>();

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Description).IsEqualTo("Groceries entry");
    }

    [Test]
    public async Task ListJournalEntries_accountId_matches_whole_subtree()
    {
        using var client = Factory.CreateClient();
        var food = await PostAccountAsync(
            client,
            new CreateAccountRequestDto($"Food-{Guid.NewGuid():N}", "Expense", "EUR")
            {
                IsPostable = false,
            }
        );
        var groceries = await PostAccountAsync(
            client,
            new CreateAccountRequestDto($"Groceries-{Guid.NewGuid():N}", "Expense", "EUR")
            {
                ParentAccountId = food.Id,
            }
        );
        var checking = await CreateAccountAsync(client, $"C-{Guid.NewGuid():N}", "Asset");

        await PostJeAsync(
            client,
            new CreateJournalEntryRequestDto(
                Date: new DateOnly(2026, 2, 3),
                Description: "Leaf entry",
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineRequestDto(groceries.Id, 1000, null),
                    new CreateJournalLineRequestDto(checking.Id, -1000, null),
                ]
            )
        );

        // Filtering on the non-postable parent matches the entry posted to its leaf.
        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries?accountId={food.Id}", UriKind.Relative)
        );
        var rows = await response.Content.ReadPagedItemsAsync<JournalEntryDto>();

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Description).IsEqualTo("Leaf entry");
    }

    [Test]
    public async Task ListJournalEntries_from_and_to_bound_the_date_inclusively()
    {
        using var client = Factory.CreateClient();
        var expense = await CreateAccountAsync(client, $"E-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"C-{Guid.NewGuid():N}", "Asset");

        var dates = new[]
        {
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 2),
            new DateOnly(2026, 3, 3),
            new DateOnly(2026, 3, 4),
        };
        foreach (var date in dates)
        {
            await PostJeAsync(
                client,
                new CreateJournalEntryRequestDto(
                    Date: date,
                    Description: null,
                    CounterpartyId: null,
                    Lines:
                    [
                        new CreateJournalLineRequestDto(expense.Id, 1000, null),
                        new CreateJournalLineRequestDto(checking.Id, -1000, null),
                    ]
                )
            );
        }

        // Scope by the freshly-minted account to stay isolated from other tests' entries.
        using var response = await client.GetAsync(
            new Uri(
                $"/api/journal-entries?accountId={expense.Id}&from=2026-03-02&to=2026-03-03",
                UriKind.Relative
            )
        );
        var rows = await response.Content.ReadPagedItemsAsync<JournalEntryDto>();

        await Assert.That(rows.Count).IsEqualTo(2);
        await Assert.That(rows[0].Date).IsEqualTo(new DateOnly(2026, 3, 3));
        await Assert.That(rows[1].Date).IsEqualTo(new DateOnly(2026, 3, 2));
    }

    [Test]
    public async Task ListJournalEntries_from_after_to_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/journal-entries?from=2026-03-04&to=2026-03-01", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListJournalEntries_q_above_max_length_returns_400()
    {
        using var client = Factory.CreateClient();
        var tooLong = new string('a', 201);
        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries?q={tooLong}", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task PostJeAsync(HttpClient client, CreateJournalEntryRequestDto request)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
    }

    private static async Task<CounterpartyDto> PostCounterpartyAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/counterparties", UriKind.Relative),
            new CreateCounterpartyRequestDto(name)
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CounterpartyDto>();
        return dto ?? throw new InvalidOperationException("Counterparty create returned no body.");
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 4000, "Albert Heijn"),
                new CreateJournalLineRequestDto(checking.Id, -4000, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Lines).Count().IsEqualTo(2);
        await Assert.That(created.Lines.Sum(l => l.Amount)).IsEqualTo(0L);

        using var getResponse = await client.GetAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative)
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 4000, null),
                new CreateJournalLineRequestDto(checking.Id, -3000, null),
            ]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
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
            CounterpartyId: null,
            Lines: [new CreateJournalLineRequestDto(account.Id, 100, null)]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateJournalEntry_currency_mismatch_returns_422()
    {
        using var client = Factory.CreateClient();

        // Only EUR is seeded; add a second currency to provoke the mismatch.
        using var createCurrencyResponse = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            new CreateCurrencyRequestDto("USD", "United States Dollar", 2, "$")
        );
        await Assert.That(createCurrencyResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var eurAccount = await CreateAccountAsync(client, "Currency-EUR", "Asset", "EUR");
        var usdAccount = await CreateAccountAsync(client, "Currency-USD", "Asset", "USD");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(eurAccount.Id, 4000, null),
                new CreateJournalLineRequestDto(usdAccount.Id, -4000, null),
            ]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task GetJournalEntry_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries/{Guid.NewGuid()}", UriKind.Relative)
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 100, null),
                new CreateJournalLineRequestDto(b.Id, -100, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/api/journal-entries/{created!.Id}", UriKind.Relative)
        );
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative)
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 500, null),
                new CreateJournalLineRequestDto(b.Id, -500, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/journal-entries/{created!.Id}", UriKind.Relative),
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 700, "original"),
                new CreateJournalLineRequestDto(b.Id, -700, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var firstLine = created!.Lines.Single(l => l.Amount == 700);

        var key = firstLine.Id.ToString("D");
        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 200, null),
                new CreateJournalLineRequestDto(b.Id, -200, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var line = created!.Lines[0];
        var key = line.Id.ToString("D");

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 300, null),
                new CreateJournalLineRequestDto(b.Id, -300, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        var fakeKey = Guid.NewGuid().ToString("D");
        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/journal-entries/{created!.Id}", UriKind.Relative),
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 900, "Albert Heijn"),
                new CreateJournalLineRequestDto(b.Id, -900, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var firstLine = created!.Lines.Single(l => l.Amount == 900);

        // Simulate a reconciliation having marked the line as Cleared by writing directly
        // through the test fixture's service scope. The PATCH below must not clobber this.
        await MarkLineClearedAsync(firstLine.Id);

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
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
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(a.Id, 100, null),
                new CreateJournalLineRequestDto(b.Id, -100, null),
            ]
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            create
        );
        var created = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        using var patchResponse = await client.PatchAsJsonPatchAsync(
            new Uri($"/api/journal-entries/{created!.Id}", UriKind.Relative),
            [JsonPatchHelpers.Replace("/bogusField", "x")]
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ReplaceJournalEntry_edits_uncleared_line_account_and_amount()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Grocery", "Expense");
        var dining = await CreateAccountAsync(client, "Put-Dining", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 10),
            "miscategorised",
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 4000, "AH"),
                new CreateJournalLineRequestDto(checking.Id, -4000, null),
            ]
        );
        var groceryLine = created.Lines.Single(l => l.Amount == 4000);
        var checkingLine = created.Lines.Single(l => l.Amount == -4000);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: new DateOnly(2026, 5, 11),
                Description: "fixed",
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(
                        groceryLine.Id,
                        dining.Id,
                        4500,
                        "AH dinner",
                        null
                    ),
                    new ReplaceJournalLineRequestDto(
                        checkingLine.Id,
                        checking.Id,
                        -4500,
                        null,
                        null
                    ),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(updated!.Id).IsEqualTo(created.Id);
        await Assert.That(updated.CreatedAt).IsEqualTo(created.CreatedAt);
        await Assert.That(updated.Date).IsEqualTo(new DateOnly(2026, 5, 11));
        await Assert.That(updated.Description).IsEqualTo("fixed");
        var movedLine = updated.Lines.Single(l => l.Id == groceryLine.Id);
        await Assert.That(movedLine.AccountId).IsEqualTo(dining.Id);
        await Assert.That(movedLine.Amount).IsEqualTo(4500L);
        await Assert.That(movedLine.Description).IsEqualTo("AH dinner");
        await Assert.That(updated.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
    }

    [Test]
    public async Task ReplaceJournalEntry_rejects_account_change_on_cleared_line()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Frozen-Grocery", "Expense");
        var dining = await CreateAccountAsync(client, "Put-Frozen-Dining", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Frozen-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 12),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 5000, null),
                new CreateJournalLineRequestDto(checking.Id, -5000, null),
            ]
        );
        var bankLine = created.Lines.Single(l => l.Amount == -5000);
        var counterLine = created.Lines.Single(l => l.Amount == 5000);
        await MarkLineClearedAsync(bankLine.Id);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(counterLine.Id, dining.Id, 5000, null, null),
                    new ReplaceJournalLineRequestDto(bankLine.Id, dining.Id, -5000, null, null),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ReplaceJournalEntry_rejects_amount_change_on_cleared_line()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Amount-Grocery", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Amount-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 13),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 1000, null),
                new CreateJournalLineRequestDto(checking.Id, -1000, null),
            ]
        );
        var bankLine = created.Lines.Single(l => l.Amount == -1000);
        var counterLine = created.Lines.Single(l => l.Amount == 1000);
        await MarkLineClearedAsync(bankLine.Id);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(counterLine.Id, grocery.Id, 1500, null, null),
                    new ReplaceJournalLineRequestDto(bankLine.Id, checking.Id, -1500, null, null),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ReplaceJournalEntry_rejects_omitting_a_cleared_line()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Omit-Grocery", "Expense");
        var dining = await CreateAccountAsync(client, "Put-Omit-Dining", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Omit-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 14),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 800, null),
                new CreateJournalLineRequestDto(checking.Id, -800, null),
            ]
        );
        var bankLine = created.Lines.Single(l => l.Amount == -800);
        var counterLine = created.Lines.Single(l => l.Amount == 800);
        await MarkLineClearedAsync(bankLine.Id);

        // Body omits the bank-side line entirely — that line is Cleared, so PUT must reject.
        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(counterLine.Id, dining.Id, 800, null, null),
                    new ReplaceJournalLineRequestDto(null, checking.Id, -800, null, null),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ReplaceJournalEntry_inserts_new_line_with_server_assigned_id()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-New-Grocery", "Expense");
        var dining = await CreateAccountAsync(client, "Put-New-Dining", "Expense");
        var checking = await CreateAccountAsync(client, "Put-New-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 15),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 3000, null),
                new CreateJournalLineRequestDto(checking.Id, -3000, null),
            ]
        );
        var groceryLine = created.Lines.Single(l => l.Amount == 3000);
        var checkingLine = created.Lines.Single(l => l.Amount == -3000);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(groceryLine.Id, grocery.Id, 2000, null, null),
                    new ReplaceJournalLineRequestDto(null, dining.Id, 1000, "split", null),
                    new ReplaceJournalLineRequestDto(
                        checkingLine.Id,
                        checking.Id,
                        -3000,
                        null,
                        null
                    ),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(updated!.Lines).Count().IsEqualTo(3);
        var newLine = updated.Lines.Single(l => l.AccountId == dining.Id);
        await Assert.That(newLine.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(newLine.Amount).IsEqualTo(1000L);
        await Assert.That(newLine.Description).IsEqualTo("split");
        await Assert.That(newLine.ReconciliationStatus).IsEqualTo("Uncleared");
    }

    [Test]
    public async Task ReplaceJournalEntry_removes_uncleared_line_omitted_from_body()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Remove-Grocery", "Expense");
        var dining = await CreateAccountAsync(client, "Put-Remove-Dining", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Remove-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 16),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 1500, null),
                new CreateJournalLineRequestDto(dining.Id, 500, null),
                new CreateJournalLineRequestDto(checking.Id, -2000, null),
            ]
        );
        var groceryLine = created.Lines.Single(l => l.AccountId == grocery.Id);
        var checkingLine = created.Lines.Single(l => l.AccountId == checking.Id);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(groceryLine.Id, grocery.Id, 2000, null, null),
                    new ReplaceJournalLineRequestDto(
                        checkingLine.Id,
                        checking.Id,
                        -2000,
                        null,
                        null
                    ),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(updated!.Lines).Count().IsEqualTo(2);
        await Assert.That(updated.Lines.All(l => l.AccountId != dining.Id)).IsTrue();
    }

    [Test]
    public async Task ReplaceJournalEntry_edits_description_on_cleared_line()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Desc-Grocery", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Desc-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 17),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 700, "AH"),
                new CreateJournalLineRequestDto(checking.Id, -700, "bank"),
            ]
        );
        var bankLine = created.Lines.Single(l => l.AccountId == checking.Id);
        var groceryLine = created.Lines.Single(l => l.AccountId == grocery.Id);
        await MarkLineClearedAsync(bankLine.Id);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(
                        groceryLine.Id,
                        grocery.Id,
                        700,
                        "AH NL",
                        null
                    ),
                    new ReplaceJournalLineRequestDto(
                        bankLine.Id,
                        checking.Id,
                        -700,
                        "renamed",
                        null
                    ),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        var bankPreserved = updated!.Lines.Single(l => l.Id == bankLine.Id);
        await Assert.That(bankPreserved.Description).IsEqualTo("renamed");
        await Assert.That(bankPreserved.ReconciliationStatus).IsEqualTo("Cleared");
    }

    [Test]
    public async Task ReplaceJournalEntry_rejects_unbalanced_body()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Unbalanced-Grocery", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Unbalanced-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 18),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 100, null),
                new CreateJournalLineRequestDto(checking.Id, -100, null),
            ]
        );
        var groceryLine = created.Lines.Single(l => l.AccountId == grocery.Id);
        var checkingLine = created.Lines.Single(l => l.AccountId == checking.Id);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(groceryLine.Id, grocery.Id, 200, null, null),
                    new ReplaceJournalLineRequestDto(
                        checkingLine.Id,
                        checking.Id,
                        -100,
                        null,
                        null
                    ),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ReplaceJournalEntry_rejects_changing_reconciliation_status()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-Status-Grocery", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Status-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 20),
            null,
            lines:
            [
                new CreateJournalLineRequestDto(grocery.Id, 250, null),
                new CreateJournalLineRequestDto(checking.Id, -250, null),
            ]
        );
        var groceryLine = created.Lines.Single(l => l.AccountId == grocery.Id);
        var checkingLine = created.Lines.Single(l => l.AccountId == checking.Id);

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: created.Date,
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(
                        groceryLine.Id,
                        grocery.Id,
                        250,
                        null,
                        "Cleared"
                    ),
                    new ReplaceJournalLineRequestDto(
                        checkingLine.Id,
                        checking.Id,
                        -250,
                        null,
                        null
                    ),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task ReplaceJournalEntry_reshapes_cash_entry_wholesale()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, "Put-Cash-Grocery", "Expense");
        var travel = await CreateAccountAsync(client, "Put-Cash-Travel", "Expense");
        var dining = await CreateAccountAsync(client, "Put-Cash-Dining", "Expense");
        var checking = await CreateAccountAsync(client, "Put-Cash-Checking", "Asset");

        var created = await CreateEntryAsync(
            client,
            new DateOnly(2026, 5, 21),
            "cash misc",
            lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 500, null),
                new CreateJournalLineRequestDto(checking.Id, -500, null),
            ]
        );

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{created.Id}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: new DateOnly(2026, 5, 22),
                Description: "wholesale reshape",
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(null, travel.Id, 300, "leg 1", null),
                    new ReplaceJournalLineRequestDto(null, dining.Id, 500, "leg 2", null),
                    new ReplaceJournalLineRequestDto(null, checking.Id, -800, null, null),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(updated!.Id).IsEqualTo(created.Id);
        await Assert.That(updated.CreatedAt).IsEqualTo(created.CreatedAt);
        await Assert.That(updated.Lines).Count().IsEqualTo(3);
        await Assert.That(updated.Lines.All(l => l.AccountId != groceries.Id)).IsTrue();
        await Assert.That(updated.Description).IsEqualTo("wholesale reshape");
        await Assert.That(updated.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
    }

    [Test]
    public async Task ReplaceJournalEntry_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();
        var grocery = await CreateAccountAsync(client, "Put-404-Grocery", "Expense");
        var checking = await CreateAccountAsync(client, "Put-404-Checking", "Asset");

        using var putResponse = await client.PutAsJsonAsync(
            new Uri($"/api/journal-entries/{Guid.NewGuid()}", UriKind.Relative),
            new ReplaceJournalEntryRequestDto(
                Date: new DateOnly(2026, 5, 23),
                Description: null,
                CounterpartyId: null,
                Lines:
                [
                    new ReplaceJournalLineRequestDto(null, grocery.Id, 100, null, null),
                    new ReplaceJournalLineRequestDto(null, checking.Id, -100, null, null),
                ]
            )
        );

        await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<JournalEntryDto> CreateEntryAsync(
        HttpClient client,
        DateOnly date,
        string? description,
        IReadOnlyList<CreateJournalLineRequestDto> lines
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: date,
            Description: description,
            CounterpartyId: null,
            Lines: lines
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        return dto!;
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
        return await PostAccountAsync(client, req);
    }

    private static async Task<AccountDto> PostAccountAsync(
        HttpClient client,
        CreateAccountRequestDto req
    )
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
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
    Guid? CounterpartyId,
    IReadOnlyList<CreateJournalLineRequestDto> Lines
);

internal sealed record CreateJournalLineRequestDto(
    Guid AccountId,
    long Amount,
    string? Description
);

internal sealed record ReplaceJournalEntryRequestDto(
    DateOnly Date,
    string? Description,
    Guid? CounterpartyId,
    IReadOnlyList<ReplaceJournalLineRequestDto> Lines
);

internal sealed record ReplaceJournalLineRequestDto(
    Guid? Id,
    Guid AccountId,
    long Amount,
    string? Description,
    string? ReconciliationStatus
);
