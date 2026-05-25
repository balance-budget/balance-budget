using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

/// <summary>
/// Verifies the filtered UNIQUE index on <c>JournalEntries.BankTransactionId</c> (per ADR 0013)
/// acts as the race backstop behind the service-layer pre-flight check: two JEs cannot reference
/// the same BankTransaction, but the filter still allows many JEs with a null link.
/// </summary>
internal sealed class JournalEntryBankTransactionIdUniqueIndexTests : EndpointsTestsBase
{
    [Test]
    public async Task CreateAsync_second_JE_for_same_BankTransaction_returns_ConflictError(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();

        var (expense, asset) = await CreateExpenseAndAssetAsync(accountService, cancellationToken);
        var bankAccount = await SeedBankAccountForAccountAsync(asset.Id, cancellationToken);
        var bankTransactionId = await SeedBankTransactionAsync(
            bankAccount.Id,
            asset.CurrencyCode,
            cancellationToken
        );

        var first = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 5, 17),
                Description: "first JE for BT",
                BankTransactionId: bankTransactionId,
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineInput(expense.Id, 4000, null),
                    new CreateJournalLineInput(asset.Id, -4000, null),
                ]
            ),
            cancellationToken
        );
        await Assert.That(first.IsSuccess).IsTrue();

        var second = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 5, 18),
                Description: "duplicate JE for same BT",
                BankTransactionId: bankTransactionId,
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineInput(expense.Id, 4000, null),
                    new CreateJournalLineInput(asset.Id, -4000, null),
                ]
            ),
            cancellationToken
        );
        await Assert.That(second.IsFailure).IsTrue();
        await Assert.That(second.Error).IsTypeOf<ConflictError>();
        await Assert.That(second.Error!.Code).IsEqualTo(ErrorCodes.UniquenessConflict);

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await dbContext.JournalEntries.CountAsync(
            j => j.BankTransactionId == bankTransactionId,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task CreateAsync_multiple_JEs_with_null_BankTransactionId_are_allowed(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();

        var (expense, asset) = await CreateExpenseAndAssetAsync(accountService, cancellationToken);

        var first = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 5, 17),
                Description: "manual #1",
                BankTransactionId: null,
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineInput(expense.Id, 1000, null),
                    new CreateJournalLineInput(asset.Id, -1000, null),
                ]
            ),
            cancellationToken
        );
        await Assert.That(first.IsSuccess).IsTrue();

        var second = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 5, 18),
                Description: "manual #2",
                BankTransactionId: null,
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineInput(expense.Id, 2000, null),
                    new CreateJournalLineInput(asset.Id, -2000, null),
                ]
            ),
            cancellationToken
        );
        await Assert.That(second.IsSuccess).IsTrue();
    }

    private static async Task<(
        AccountOutput Expense,
        AccountOutput Asset
    )> CreateExpenseAndAssetAsync(
        IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var expenseResult = await accountService.CreateAsync(
            $"Groceries-uidx-{Guid.NewGuid():N}",
            AccountType.Expense,
            new CurrencyCode("EUR"),
            cancellationToken
        );
        await Assert.That(expenseResult.IsSuccess).IsTrue();

        var assetResult = await accountService.CreateAsync(
            $"Checking-uidx-{Guid.NewGuid():N}",
            AccountType.Asset,
            new CurrencyCode("EUR"),
            cancellationToken
        );
        await Assert.That(assetResult.IsSuccess).IsTrue();

        return (expenseResult.Value!, assetResult.Value!);
    }

    private async Task<BankAccountOutput> SeedBankAccountForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var result = await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Iban: $"NL00BANK{Guid.NewGuid():N}"[..18],
                AccountNumber: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: new CurrencyCode("EUR"),
                AccountId: accountId,
                CounterpartyId: null
            ),
            cancellationToken
        );
        await Assert.That(result.IsSuccess).IsTrue();
        return result.Value!;
    }

    private async Task<BankTransactionId> SeedBankTransactionAsync(
        BankAccountId bankAccountId,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var now = DateTime.UtcNow;
        var entity = new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = bankAccountId,
            BookingDate = new DateOnly(2026, 5, 17),
            Money = new Money(4000, currencyCode),
            Description = "seed BT for unique-index test",
            CounterpartyName = null,
            CounterpartyAccountNumber = null,
            RawSource = $"test-{Guid.NewGuid():N}",
            RowHash = $"hash-{Guid.NewGuid():N}",
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.BankTransactions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
