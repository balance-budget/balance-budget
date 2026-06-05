using System.Net;
using System.Net.Http.Json;
using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Api;

/// <summary>
/// Covers POST /api/journal-lines/reassign: the all-or-nothing bulk move of Uncleared
/// JournalLines to a postable, currency-matching target Account (see CONTEXT.md "Reassign").
/// </summary>
internal sealed class JournalLineReassignEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task Reassign_moves_uncleared_lines_to_the_target_account()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, $"RA-Groc-{Guid.NewGuid():N}", "Expense");
        var food = await CreateAccountAsync(client, $"RA-Food-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"RA-Chk-{Guid.NewGuid():N}", "Asset");

        var first = await PostJeAsync(client, groceries.Id, checking.Id, 3_000L);
        var second = await PostJeAsync(client, groceries.Id, checking.Id, 4_500L);
        var lineIds = new[]
        {
            first.Lines.Single(l => l.AccountId == groceries.Id).Id,
            second.Lines.Single(l => l.AccountId == groceries.Id).Id,
        };

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto(lineIds, food.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        var reloaded = await GetJeAsync(client, first.Id);
        await Assert
            .That(reloaded.Lines.Single(l => l.Id == lineIds[0]).AccountId)
            .IsEqualTo(food.Id);
        var reloadedSecond = await GetJeAsync(client, second.Id);
        await Assert
            .That(reloadedSecond.Lines.Single(l => l.Id == lineIds[1]).AccountId)
            .IsEqualTo(food.Id);
    }

    [Test]
    public async Task Reassign_with_a_frozen_line_rejects_the_whole_batch()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, $"RF-Groc-{Guid.NewGuid():N}", "Expense");
        var food = await CreateAccountAsync(client, $"RF-Food-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"RF-Chk-{Guid.NewGuid():N}", "Asset");

        var movable = await PostJeAsync(client, groceries.Id, checking.Id, 3_000L);
        var frozen = await PostJeAsync(client, groceries.Id, checking.Id, 4_500L);
        var movableLineId = movable.Lines.Single(l => l.AccountId == groceries.Id).Id;
        var frozenLineId = frozen.Lines.Single(l => l.AccountId == groceries.Id).Id;
        await MarkLineAsync(frozenLineId, ReconciliationStatus.Cleared);

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto([movableLineId, frozenLineId], food.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
        // All-or-nothing: the movable line must not have moved either.
        var reloaded = await GetJeAsync(client, movable.Id);
        await Assert
            .That(reloaded.Lines.Single(l => l.Id == movableLineId).AccountId)
            .IsEqualTo(groceries.Id);
    }

    [Test]
    public async Task Reassign_to_a_non_postable_target_returns_422()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, $"RP-Groc-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"RP-Chk-{Guid.NewGuid():N}", "Asset");
        var parent = await PostAccountAsync(
            client,
            new CreateAccountRequestDto($"RP-Parent-{Guid.NewGuid():N}", "Expense", "EUR")
            {
                IsPostable = false,
            }
        );

        var entry = await PostJeAsync(client, groceries.Id, checking.Id, 3_000L);
        var lineId = entry.Lines.Single(l => l.AccountId == groceries.Id).Id;

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto([lineId], parent.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task Reassign_to_a_target_in_another_currency_returns_422()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, $"RC-Groc-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"RC-Chk-{Guid.NewGuid():N}", "Asset");
        var currency = await CreateIsolatedCurrencyAsync(client);
        var foreign = await CreateAccountAsync(
            client,
            $"RC-Foreign-{Guid.NewGuid():N}",
            "Expense",
            currency
        );

        var entry = await PostJeAsync(client, groceries.Id, checking.Id, 3_000L);
        var lineId = entry.Lines.Single(l => l.AccountId == groceries.Id).Id;

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto([lineId], foreign.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task Reassign_unknown_line_returns_404()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAccountAsync(client, $"RU-Food-{Guid.NewGuid():N}", "Expense");

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto([Guid.NewGuid()], food.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Reassign_unknown_target_account_returns_404()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(client, $"RT-Groc-{Guid.NewGuid():N}", "Expense");
        var checking = await CreateAccountAsync(client, $"RT-Chk-{Guid.NewGuid():N}", "Asset");

        var entry = await PostJeAsync(client, groceries.Id, checking.Id, 3_000L);
        var lineId = entry.Lines.Single(l => l.AccountId == groceries.Id).Id;

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto([lineId], Guid.NewGuid())
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Reassign_with_empty_line_ids_returns_400()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAccountAsync(client, $"RE-Food-{Guid.NewGuid():N}", "Expense");

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto([], food.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Reassign_above_the_batch_cap_returns_400()
    {
        using var client = Factory.CreateClient();
        var food = await CreateAccountAsync(client, $"RB-Food-{Guid.NewGuid():N}", "Expense");
        var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

        using var response = await client.PostAsJsonAsync(
            ReassignUri,
            new ReassignRequestDto(tooMany, food.Id)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static readonly Uri ReassignUri = new("/api/journal-lines/reassign", UriKind.Relative);

    private static async Task<JournalEntryDto> PostJeAsync(
        HttpClient client,
        Guid debitAccountId,
        Guid creditAccountId,
        long amount
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 30),
            Description: null,
            CounterpartyId: null,
            Lines:
            [
                new CreateJournalLineRequestDto(debitAccountId, amount, null),
                new CreateJournalLineRequestDto(creditAccountId, -amount, null),
            ]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        return dto!;
    }

    private static async Task<JournalEntryDto> GetJeAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(
            new Uri($"/api/journal-entries/{id}", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        return dto!;
    }

    private async Task MarkLineAsync(Guid lineId, ReconciliationStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var line = await dbContext.JournalLines.SingleAsync(l => l.Id == new JournalLineId(lineId));
        line.ReconciliationStatus = status;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
        string currencyCode = "EUR"
    )
    {
        return await PostAccountAsync(
            client,
            new CreateAccountRequestDto(name, accountType, currencyCode)
        );
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

    private static async Task<string> CreateIsolatedCurrencyAsync(HttpClient client)
    {
        var code = ("Y" + Guid.NewGuid().ToString("N")[..4]).ToUpperInvariant();
        var request = new CreateCurrencyRequestDto(code, $"Test {code}", 2, null);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/currencies", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        return code;
    }
}

internal sealed record ReassignRequestDto(IReadOnlyList<Guid> LineIds, Guid TargetAccountId);
