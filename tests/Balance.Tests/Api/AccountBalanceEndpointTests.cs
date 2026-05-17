using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class AccountBalanceEndpointTests : EndpointsTestsBase
{
    [Test]
    public async Task GetBalance_for_unknown_account_returns_404()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/accounts/{Guid.NewGuid()}/balance", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetBalance_for_account_with_no_lines_is_zero_in_account_currency()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, $"Empty-{Guid.NewGuid():N}", "Asset", "EUR");

        var balance = await GetBalanceAsync(client, account.Id);

        await Assert.That(balance.Amount).IsEqualTo(0L);
        await Assert.That(balance.CurrencyCode).IsEqualTo("EUR");
    }

    [Test]
    public async Task GetBalance_balanced_expense_moves_bank_down_and_expense_up()
    {
        using var client = Factory.CreateClient();
        var groceries = await CreateAccountAsync(
            client,
            $"Groceries-Bal-{Guid.NewGuid():N}",
            "Expense"
        );
        var checking = await CreateAccountAsync(
            client,
            $"Checking-Bal-{Guid.NewGuid():N}",
            "Asset"
        );

        await PostJournalEntryAsync(
            client,
            lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 4000, null),
                new CreateJournalLineRequestDto(checking.Id, -4000, null),
            ]
        );

        var groceriesBalance = await GetBalanceAsync(client, groceries.Id);
        var checkingBalance = await GetBalanceAsync(client, checking.Id);

        // Asset / Expense are debit-normal: balance = SUM(Amount)
        await Assert.That(groceriesBalance.Amount).IsEqualTo(4000L);
        await Assert.That(checkingBalance.Amount).IsEqualTo(-4000L);
        await Assert.That(groceriesBalance.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(checkingBalance.CurrencyCode).IsEqualTo("EUR");
    }

    [Test]
    public async Task GetBalance_credit_normal_account_returns_negated_sum()
    {
        using var client = Factory.CreateClient();
        var salary = await CreateAccountAsync(
            client,
            $"Salary-{Guid.NewGuid():N}",
            "Income"
        );
        var checking = await CreateAccountAsync(
            client,
            $"Salary-Checking-{Guid.NewGuid():N}",
            "Asset"
        );

        // Salary lands in checking: debit checking +250000, credit salary -250000.
        await PostJournalEntryAsync(
            client,
            lines:
            [
                new CreateJournalLineRequestDto(checking.Id, 250000, null),
                new CreateJournalLineRequestDto(salary.Id, -250000, null),
            ]
        );

        var salaryBalance = await GetBalanceAsync(client, salary.Id);
        var checkingBalance = await GetBalanceAsync(client, checking.Id);

        // Income is credit-normal: balance = -SUM(Amount) = -(-250000) = 250000 (positive).
        await Assert.That(salaryBalance.Amount).IsEqualTo(250000L);
        // Asset is debit-normal: balance = SUM(Amount) = 250000.
        await Assert.That(checkingBalance.Amount).IsEqualTo(250000L);
    }

    [Test]
    public async Task GetBalance_credit_normal_liability_returns_negated_sum()
    {
        using var client = Factory.CreateClient();
        var creditCard = await CreateAccountAsync(
            client,
            $"CreditCard-{Guid.NewGuid():N}",
            "Liability"
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Groceries-CC-{Guid.NewGuid():N}",
            "Expense"
        );

        // Spending on the card: debit groceries +5000, credit card -5000.
        await PostJournalEntryAsync(
            client,
            lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 5000, null),
                new CreateJournalLineRequestDto(creditCard.Id, -5000, null),
            ]
        );

        var cardBalance = await GetBalanceAsync(client, creditCard.Id);

        // Liability is credit-normal: balance = -SUM(Amount) = -(-5000) = 5000 (amount owed).
        await Assert.That(cardBalance.Amount).IsEqualTo(5000L);
    }

    [Test]
    public async Task GetBalance_sums_across_multiple_entries()
    {
        using var client = Factory.CreateClient();
        var checking = await CreateAccountAsync(
            client,
            $"Checking-Multi-{Guid.NewGuid():N}",
            "Asset"
        );
        var groceries = await CreateAccountAsync(
            client,
            $"Groceries-Multi-{Guid.NewGuid():N}",
            "Expense"
        );

        await PostJournalEntryAsync(
            client,
            lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 1000, null),
                new CreateJournalLineRequestDto(checking.Id, -1000, null),
            ]
        );
        await PostJournalEntryAsync(
            client,
            lines:
            [
                new CreateJournalLineRequestDto(groceries.Id, 2500, null),
                new CreateJournalLineRequestDto(checking.Id, -2500, null),
            ]
        );

        var groceriesBalance = await GetBalanceAsync(client, groceries.Id);
        var checkingBalance = await GetBalanceAsync(client, checking.Id);

        await Assert.That(groceriesBalance.Amount).IsEqualTo(3500L);
        await Assert.That(checkingBalance.Amount).IsEqualTo(-3500L);
    }

    private static async Task<MoneyDto> GetBalanceAsync(HttpClient client, Guid accountId)
    {
        using var response = await client.GetAsync(
            new Uri($"/accounts/{accountId}/balance", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<MoneyDto>();
        return dto!;
    }

    private static async Task<AccountDto> CreateAccountAsync(
        HttpClient client,
        string name,
        string accountType,
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

    private static async Task PostJournalEntryAsync(
        HttpClient client,
        IReadOnlyList<CreateJournalLineRequestDto> lines
    )
    {
        var request = new CreateJournalEntryRequestDto(
            Date: new DateOnly(2026, 5, 17),
            Description: null,
            BankTransactionId: null,
            CounterpartyId: null,
            Lines: lines
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
    }
}
