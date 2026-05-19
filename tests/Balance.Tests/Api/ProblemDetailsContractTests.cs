using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class ProblemDetailsContractTests : EndpointsTestsBase
{
    [Test]
    public async Task NotFound_returns_problem_details_with_type_and_instance()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/api/accounts/{Guid.NewGuid()}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert
            .That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(404);
        await Assert.That(problem.Type).IsNotNull();
        await Assert.That(problem.Type!).StartsWith("https://");
        await Assert.That(problem.Instance).IsNotNull();
        await Assert.That(problem.Instance!).IsNotEmpty();
    }

    [Test]
    public async Task Conflict_returns_problem_details_with_status_409()
    {
        using var client = Factory.CreateClient();

        var first = new CreateAccountRequestDto($"DupName-{Guid.NewGuid():N}", "Expense", "EUR");
        using var firstResponse = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            first
        );
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var duplicate = new CreateAccountRequestDto(first.Name, "Expense", "EUR");
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            duplicate
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert
            .That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(409);
        await Assert.That(problem.Instance).IsNotNull();
    }

    [Test]
    public async Task Invariant_violation_returns_422_problem_details()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(
            client,
            $"PD-Groceries-{Guid.NewGuid():N}",
            "Expense"
        );
        var checking = await CreateAccountAsync(client, $"PD-Checking-{Guid.NewGuid():N}", "Asset");

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
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
        await Assert
            .That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(422);
    }

    [Test]
    public async Task Validation_failure_returns_400_with_errors_dictionary()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, $"PD-Single-{Guid.NewGuid():N}", "Asset");

        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines: [new CreateJournalLineRequestDto(account.Id, 100, null)]
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert
            .That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetailsDto>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(400);
        await Assert.That(problem.Errors).IsNotNull();
        await Assert.That(problem.Errors!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task OpenApi_document_lists_problem_details_responses_for_account_get()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/openapi/v1.json", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var responses = document
            .RootElement.GetProperty("paths")
            .GetProperty("/api/accounts/{id}")
            .GetProperty("get")
            .GetProperty("responses");

        await Assert.That(responses.TryGetProperty("404", out _)).IsTrue();

        var responses400 = document
            .RootElement.GetProperty("paths")
            .GetProperty("/api/accounts")
            .GetProperty("post")
            .GetProperty("responses");

        await Assert.That(responses400.TryGetProperty("400", out _)).IsTrue();
        await Assert.That(responses400.TryGetProperty("409", out _)).IsTrue();
        await Assert.That(responses400.TryGetProperty("422", out _)).IsTrue();
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
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }
}

internal sealed record ProblemDetailsDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance
);

internal sealed record ValidationProblemDetailsDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    IReadOnlyDictionary<string, string[]>? Errors
);
