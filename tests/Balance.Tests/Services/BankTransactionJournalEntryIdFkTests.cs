using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

/// <summary>
/// Verifies the flipped FK shape from ADR 0012: the link lives on
/// <c>BankTransaction.JournalEntryId</c> as a nullable scalar FK with
/// <c>ON DELETE SET NULL</c>. The cardinality is N BTs per JE (reserved
/// for self-transfer Attach — see ADR 0012); today every JE has 0 or 1
/// referencing BT, but the schema permits multiple.
/// </summary>
internal sealed class BankTransactionJournalEntryIdFkTests : EndpointsTestsBase
{
    [Test]
    public async Task Categorizing_a_BT_sets_BankTransaction_JournalEntryId_to_new_entry(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var bankTransactionService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionService>();
        var categorizationService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionCategorizationService>();

        var (expense, asset) = await CreateExpenseAndAssetAsync(accountService, cancellationToken);
        var bankAccount = await SeedBankAccountForAccountAsync(
            bankAccountService,
            asset.Id,
            cancellationToken
        );
        var btx = await CreateBankTransactionAsync(
            bankTransactionService,
            bankAccount.Id,
            asset.CurrencyCode,
            amount: -4000,
            cancellationToken
        );

        var categorized = await categorizationService.CategorizeAsync(
            btx.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: null,
                Date: new DateOnly(2026, 5, 17),
                Description: "fk-flip",
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 4000, null)]
            ),
            cancellationToken
        );
        await Assert.That(categorized.IsSuccess).IsTrue();

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var btxRow = await dbContext.BankTransactions.SingleAsync(
            b => b.Id == btx.Id,
            cancellationToken
        );
        await Assert.That(btxRow.JournalEntryId).IsEqualTo(categorized.Value!.Id);
        await Assert.That(categorized.Value!.BankTransactions).Count().IsEqualTo(1);
        await Assert.That(categorized.Value!.BankTransactions[0].Id).IsEqualTo(btx.Id);
    }

    [Test]
    public async Task Deleting_a_JE_nulls_the_referencing_BTs_JournalEntryId(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var bankTransactionService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionService>();
        var categorizationService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionCategorizationService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();

        var (expense, asset) = await CreateExpenseAndAssetAsync(accountService, cancellationToken);
        var bankAccount = await SeedBankAccountForAccountAsync(
            bankAccountService,
            asset.Id,
            cancellationToken
        );
        var btx = await CreateBankTransactionAsync(
            bankTransactionService,
            bankAccount.Id,
            asset.CurrencyCode,
            amount: -4000,
            cancellationToken
        );

        var categorized = await categorizationService.CategorizeAsync(
            btx.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: null,
                Date: new DateOnly(2026, 5, 17),
                Description: "to-delete",
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 4000, null)]
            ),
            cancellationToken
        );
        await Assert.That(categorized.IsSuccess).IsTrue();

        var deleteResult = await journalEntryService.DeleteAsync(
            categorized.Value!.Id,
            cancellationToken
        );
        await Assert.That(deleteResult.IsSuccess).IsTrue();

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var btxRow = await dbContext.BankTransactions.SingleAsync(
            b => b.Id == btx.Id,
            cancellationToken
        );
        await Assert.That(btxRow.JournalEntryId).IsNull();
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
            $"Groceries-fk-{Guid.NewGuid():N}",
            AccountType.Expense,
            new CurrencyCode("EUR"),
            cancellationToken
        );
        await Assert.That(expenseResult.IsSuccess).IsTrue();

        var assetResult = await accountService.CreateAsync(
            $"Checking-fk-{Guid.NewGuid():N}",
            AccountType.Asset,
            new CurrencyCode("EUR"),
            cancellationToken
        );
        await Assert.That(assetResult.IsSuccess).IsTrue();

        return (expenseResult.Value!, assetResult.Value!);
    }

    private static async Task<BankAccountOutput> SeedBankAccountForAccountAsync(
        IBankAccountService bankAccountService,
        AccountId accountId,
        CancellationToken cancellationToken
    )
    {
        var result = await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Type: BankAccountType.Current,
                Iban: $"NL00BANK{Guid.NewGuid():N}"[..18],
                AccountNumber: null,
                CardIdentifier: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: new CurrencyCode("EUR"),
                ImporterKey: null,
                AccountId: accountId,
                CounterpartyId: null
            ),
            cancellationToken
        );
        await Assert.That(result.IsSuccess).IsTrue();
        return result.Value!;
    }

    private static async Task<BankTransactionOutput> CreateBankTransactionAsync(
        IBankTransactionService bankTransactionService,
        BankAccountId bankAccountId,
        CurrencyCode currencyCode,
        long amount,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.CreateAsync(
            new CreateBankTransactionInput(
                BankAccountId: bankAccountId,
                BookingDate: new DateOnly(2026, 5, 17),
                Amount: amount,
                CurrencyCode: currencyCode,
                Description: "fk-test",
                CounterpartyName: null,
                CounterpartyAccountNumber: null
            ),
            cancellationToken
        );
        await Assert.That(result.IsSuccess).IsTrue();
        return result.Value!;
    }
}
