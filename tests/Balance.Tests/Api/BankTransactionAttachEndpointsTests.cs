using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

/// <summary>
/// HTTP-level coverage of the Attach feature (issue #93 / ADR 0013):
///  * POST /api/bank-transactions/{id}/attach
///  * POST /api/bank-transactions/{id}/detach
///  * GET  /api/bank-transactions/{id}/attach-candidates
///  * Inbox list endpoint surfaces a MatchingJournalEntry hint when the predicate uniquely matches.
/// </summary>
internal sealed class BankTransactionAttachEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task Attach_round_trip_flips_line_to_cleared_and_links_BT(
        CancellationToken cancellationToken
    )
    {
        using var client = Factory.CreateClient();
        var scenario = await SeedSelfTransferScenarioAsync(client);

        using var attach = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}/attach", UriKind.Relative),
            new { JournalEntryId = scenario.SelfTransferJeId },
            cancellationToken
        );

        await Assert.That(attach.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var entry = await attach.Content.ReadFromJsonAsync<JournalEntryDto>(cancellationToken);
        await Assert.That(entry!.Lines).Count().IsEqualTo(2);
        await Assert.That(entry.Lines.All(l => l.ReconciliationStatus == "Cleared")).IsTrue();

        using var sibling = await client.GetAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}", UriKind.Relative),
            cancellationToken
        );
        var siblingDto = await sibling.Content.ReadFromJsonAsync<BankTransactionDto>(
            cancellationToken
        );
        await Assert.That(siblingDto!.JournalEntryId).IsEqualTo(scenario.SelfTransferJeId);
    }

    [Test]
    public async Task Attach_rejects_when_predicate_fails(CancellationToken cancellationToken)
    {
        using var client = Factory.CreateClient();
        // Sibling has wrong CounterpartyAccountNumber — predicate should reject.
        var scenario = await SeedSelfTransferScenarioAsync(
            client,
            siblingCounterpartyAccountNumber: "NL00WRONG1234567890"
        );

        using var attach = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}/attach", UriKind.Relative),
            new { JournalEntryId = scenario.SelfTransferJeId },
            cancellationToken
        );

        await Assert.That(attach.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task Detach_round_trip_unlinks_BT_and_flips_line_back_to_uncleared(
        CancellationToken cancellationToken
    )
    {
        using var client = Factory.CreateClient();
        var scenario = await SeedSelfTransferScenarioAsync(client);

        using var attach = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}/attach", UriKind.Relative),
            new { JournalEntryId = scenario.SelfTransferJeId },
            cancellationToken
        );
        await Assert.That(attach.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var detach = await client.PostAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}/detach", UriKind.Relative),
            content: null,
            cancellationToken
        );
        await Assert.That(detach.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var entry = await detach.Content.ReadFromJsonAsync<JournalEntryDto>(cancellationToken);
        await Assert.That(entry!.Lines.Any(l => l.ReconciliationStatus == "Uncleared")).IsTrue();

        using var sibling = await client.GetAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}", UriKind.Relative),
            cancellationToken
        );
        var siblingDto = await sibling.Content.ReadFromJsonAsync<BankTransactionDto>(
            cancellationToken
        );
        await Assert.That(siblingDto!.JournalEntryId).IsNull();
    }

    [Test]
    public async Task Detach_rejects_when_not_attached(CancellationToken cancellationToken)
    {
        using var client = Factory.CreateClient();
        var scenario = await SeedSelfTransferScenarioAsync(client);

        using var detach = await client.PostAsync(
            new Uri($"/api/bank-transactions/{scenario.SiblingBtId}/detach", UriKind.Relative),
            content: null,
            cancellationToken
        );

        await Assert.That(detach.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task Inbox_list_surfaces_MatchingJournalEntry_hint_when_unique_match(
        CancellationToken cancellationToken
    )
    {
        using var client = Factory.CreateClient();
        var scenario = await SeedSelfTransferScenarioAsync(client);

        using var list = await client.GetAsync(
            new Uri("/api/bank-transactions?filter=Inbox&take=200", UriKind.Relative),
            cancellationToken
        );
        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var rows = await list.Content.ReadFromJsonAsync<IReadOnlyList<BankTransactionWithHintDto>>(
            cancellationToken
        );
        var siblingRow = rows!.Single(r => r.Id == scenario.SiblingBtId);
        await Assert.That(siblingRow.MatchingJournalEntry).IsNotNull();
        await Assert.That(siblingRow.MatchingJournalEntry!.Id).IsEqualTo(scenario.SelfTransferJeId);
    }

    [Test]
    public async Task AttachCandidates_returns_structural_matches_in_widened_window(
        CancellationToken cancellationToken
    )
    {
        using var client = Factory.CreateClient();
        // 10 days off — outside strict 3-day predicate, inside the manual 14-day window.
        var scenario = await SeedSelfTransferScenarioAsync(
            client,
            siblingBookingDate: new DateOnly(2026, 5, 27)
        );

        using var list = await client.GetAsync(
            new Uri(
                $"/api/bank-transactions/{scenario.SiblingBtId}/attach-candidates?dateWindowDays=14",
                UriKind.Relative
            ),
            cancellationToken
        );

        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var candidates = await list.Content.ReadFromJsonAsync<IReadOnlyList<AttachCandidateDto>>(
            cancellationToken
        );
        await Assert.That(candidates!.Any(c => c.Id == scenario.SelfTransferJeId)).IsTrue();
    }

    private static async Task<SelfTransferScenarioDto> SeedSelfTransferScenarioAsync(
        HttpClient client,
        DateOnly? siblingBookingDate = null,
        long? siblingAmount = null,
        string? siblingCounterpartyAccountNumber = null
    )
    {
        var currentAccount = await CreateAccountAsync(client, "Checking-attach-api");
        var savingsAccount = await CreateAccountAsync(client, "Savings-attach-api");

        var unique = Guid.NewGuid().ToString("N").ToUpperInvariant().Substring(0, 6);
        var currentIban = $"NL01ATCH{unique}0001"[..18];
        var savingsIban = $"NL02ATCH{unique}0002"[..18];

        var currentBank = await CreateOwnedBankAccountAsync(client, currentIban, currentAccount.Id);
        var savingsBank = await CreateOwnedBankAccountAsync(client, savingsIban, savingsAccount.Id);

        // BT-A: from current account, categorise as self-transfer to savings.
        var btARequest = new CreateBankTransactionRequestDto(
            BankAccountId: currentBank.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: -25000L,
            CurrencyCode: "EUR",
            Description: "Transfer to savings",
            CounterpartyName: null,
            CounterpartyAccountNumber: savingsIban
        );
        using var btAResponse = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            btARequest
        );
        btAResponse.EnsureSuccessStatusCode();
        var btA = await btAResponse.Content.ReadFromJsonAsync<BankTransactionDto>();

        using var categorize = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btA!.Id}/categorize", UriKind.Relative),
            new
            {
                CounterpartyId = (Guid?)null,
                NewCounterparty = (object?)null,
                Date = new DateOnly(2026, 5, 17),
                Description = "Self-transfer",
                Lines = new[]
                {
                    new
                    {
                        AccountId = savingsAccount.Id,
                        Amount = 25000L,
                        Description = (string?)null,
                    },
                },
            }
        );
        categorize.EnsureSuccessStatusCode();
        var je = await categorize.Content.ReadFromJsonAsync<JournalEntryDto>();

        // BT-B: on savings account.
        var btBRequest = new CreateBankTransactionRequestDto(
            BankAccountId: savingsBank.Id,
            BookingDate: siblingBookingDate ?? new DateOnly(2026, 5, 18),
            Amount: siblingAmount ?? 25000L,
            CurrencyCode: "EUR",
            Description: "Inbound from current",
            CounterpartyName: null,
            CounterpartyAccountNumber: siblingCounterpartyAccountNumber ?? currentIban
        );
        using var btBResponse = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            btBRequest
        );
        btBResponse.EnsureSuccessStatusCode();
        var btB = await btBResponse.Content.ReadFromJsonAsync<BankTransactionDto>();

        return new SelfTransferScenarioDto(je!.Id, btB!.Id);
    }

    private static async Task<AccountDto> CreateAccountAsync(HttpClient client, string namePrefix)
    {
        var req = new CreateAccountRequestDto($"{namePrefix}-{Guid.NewGuid():N}", "Asset", "EUR");
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task<BankAccountDto> CreateOwnedBankAccountAsync(
        HttpClient client,
        string iban,
        Guid accountId
    )
    {
        var req = new CreateBankAccountRequestDto(
            Iban: iban,
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: accountId,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        return dto!;
    }
}

internal sealed record SelfTransferScenarioDto(Guid SelfTransferJeId, Guid SiblingBtId);

internal sealed record BankTransactionWithHintDto(
    Guid Id,
    Guid? JournalEntryId,
    AttachHintDto? MatchingJournalEntry
);

internal sealed record AttachHintDto(
    Guid Id,
    DateOnly Date,
    string? Description,
    string OtherAccountName
);

internal sealed record AttachCandidateDto(
    Guid Id,
    DateOnly Date,
    string? Description,
    string OtherAccountName,
    long Amount
);
