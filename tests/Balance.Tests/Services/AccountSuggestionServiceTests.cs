using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class AccountSuggestionServiceTests : EndpointsTestsBase
{
    [Test]
    public async Task GetSuggestedCounterAccountsAsync_returns_NotFound_for_unknown_counterparty(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAccountSuggestionService>();

        var result = await service.GetSuggestedCounterAccountsAsync(
            new CounterpartyId(Guid.NewGuid()),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<NotFoundError>();
    }

    [Test]
    public async Task GetSuggestedCounterAccountsAsync_returns_empty_when_no_prior_entry(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var service = scope.ServiceProvider.GetRequiredService<IAccountSuggestionService>();

        var counterparty = (
            await counterpartyService.CreateAsync(
                $"Suggest-empty-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;

        var result = await service.GetSuggestedCounterAccountsAsync(
            counterparty.Id,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!).IsEmpty();
    }

    [Test]
    public async Task GetSuggestedCounterAccountsAsync_returns_counter_side_only_for_two_line_entry(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();
        var service = scope.ServiceProvider.GetRequiredService<IAccountSuggestionService>();

        var checking = (
            await accountService.CreateAsync(
                $"Checking-sug-{Guid.NewGuid():N}",
                AccountType.Asset,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;
        var groceries = (
            await accountService.CreateAsync(
                $"Groceries-sug-{Guid.NewGuid():N}",
                AccountType.Expense,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;

        await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Iban: $"NL69INGB{NextDigits(10)}",
                AccountNumber: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: new CurrencyCode("EUR"),
                AccountId: checking.Id,
                CounterpartyId: null
            ),
            cancellationToken
        );

        var counterparty = (
            await counterpartyService.CreateAsync($"AH-sug-{Guid.NewGuid():N}", cancellationToken)
        ).Value!;

        await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 4, 10),
                Description: "AH",
                CounterpartyId: counterparty.Id,
                Lines:
                [
                    new CreateJournalLineInput(checking.Id, -4000, null),
                    new CreateJournalLineInput(groceries.Id, 4000, null),
                ]
            ),
            cancellationToken
        );

        var result = await service.GetSuggestedCounterAccountsAsync(
            counterparty.Id,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!).Count().IsEqualTo(1);
        await Assert.That(result.Value![0].AccountId).IsEqualTo(groceries.Id);
        await Assert.That(result.Value![0].Amount).IsEqualTo(4000L);
    }

    [Test]
    public async Task GetSuggestedCounterAccountsAsync_returns_split_shape_for_three_line_entry(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();
        var service = scope.ServiceProvider.GetRequiredService<IAccountSuggestionService>();

        var checking = (
            await accountService.CreateAsync(
                $"Checking-split-{Guid.NewGuid():N}",
                AccountType.Asset,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;
        var groceries = (
            await accountService.CreateAsync(
                $"Groceries-split-{Guid.NewGuid():N}",
                AccountType.Expense,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;
        var household = (
            await accountService.CreateAsync(
                $"Household-split-{Guid.NewGuid():N}",
                AccountType.Expense,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;

        await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Iban: $"NL69INGB{NextDigits(10)}",
                AccountNumber: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: new CurrencyCode("EUR"),
                AccountId: checking.Id,
                CounterpartyId: null
            ),
            cancellationToken
        );

        var counterparty = (
            await counterpartyService.CreateAsync($"AH-split-{Guid.NewGuid():N}", cancellationToken)
        ).Value!;

        await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 4, 12),
                Description: "AH split",
                CounterpartyId: counterparty.Id,
                Lines:
                [
                    new CreateJournalLineInput(checking.Id, -8743, null),
                    new CreateJournalLineInput(groceries.Id, 6000, null),
                    new CreateJournalLineInput(household.Id, 2743, null),
                ]
            ),
            cancellationToken
        );

        var result = await service.GetSuggestedCounterAccountsAsync(
            counterparty.Id,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!).Count().IsEqualTo(2);
        var groceriesSuggestion = result.Value!.Single(s => s.AccountId == groceries.Id);
        var householdSuggestion = result.Value!.Single(s => s.AccountId == household.Id);
        await Assert.That(groceriesSuggestion.Amount).IsEqualTo(6000L);
        await Assert.That(householdSuggestion.Amount).IsEqualTo(2743L);
    }

    [Test]
    public async Task GetSuggestedCounterAccountsAsync_picks_most_recent_by_date_then_created_at(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();
        var service = scope.ServiceProvider.GetRequiredService<IAccountSuggestionService>();

        var checking = (
            await accountService.CreateAsync(
                $"Checking-recent-{Guid.NewGuid():N}",
                AccountType.Asset,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;
        var groceries = (
            await accountService.CreateAsync(
                $"Groceries-recent-{Guid.NewGuid():N}",
                AccountType.Expense,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;
        var dining = (
            await accountService.CreateAsync(
                $"Dining-recent-{Guid.NewGuid():N}",
                AccountType.Expense,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;

        await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Iban: $"NL69INGB{NextDigits(10)}",
                AccountNumber: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: new CurrencyCode("EUR"),
                AccountId: checking.Id,
                CounterpartyId: null
            ),
            cancellationToken
        );

        var counterparty = (
            await counterpartyService.CreateAsync(
                $"CP-recent-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;

        await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 1, 5),
                Description: "older",
                CounterpartyId: counterparty.Id,
                Lines:
                [
                    new CreateJournalLineInput(checking.Id, -1500, null),
                    new CreateJournalLineInput(groceries.Id, 1500, null),
                ]
            ),
            cancellationToken
        );

        await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 4, 30),
                Description: "newer",
                CounterpartyId: counterparty.Id,
                Lines:
                [
                    new CreateJournalLineInput(checking.Id, -2500, null),
                    new CreateJournalLineInput(dining.Id, 2500, null),
                ]
            ),
            cancellationToken
        );

        var result = await service.GetSuggestedCounterAccountsAsync(
            counterparty.Id,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!).Count().IsEqualTo(1);
        await Assert.That(result.Value![0].AccountId).IsEqualTo(dining.Id);
        await Assert.That(result.Value![0].Amount).IsEqualTo(2500L);
    }

    private static string NextDigits(int length)
    {
        var digits = new char[length];
        for (var i = 0; i < length; i++)
            digits[i] = (char)(
                '0' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10)
            );
        return new string(digits);
    }
}
