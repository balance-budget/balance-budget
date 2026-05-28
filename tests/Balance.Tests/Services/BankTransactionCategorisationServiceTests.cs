using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class BankTransactionCategorisationServiceTests : EndpointsTestsBase
{
    [Test]
    public async Task CategorizeAsync_happy_path_with_existing_counterparty_creates_balanced_entry(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"AH-cat-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;

        var groceries = await fixture.CreateAccountAsync(
            "Groceries-cat",
            AccountType.Expense,
            cancellationToken
        );

        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -4000,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: "AH groceries",
                Lines: [new CategorizeBankTransactionLineInput(groceries.Id, 4000, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var entry = result.Value!;
        await Assert.That(entry.BankTransactions).Count().IsEqualTo(1);
        await Assert.That(entry.BankTransactions[0].Id).IsEqualTo(bankTransaction.Id);
        await Assert.That(entry.BankTransactions[0].JournalEntryId).IsEqualTo(entry.Id);
        await Assert.That(entry.CounterpartyId).IsEqualTo(counterparty.Id);
        await Assert.That(entry.Lines).Count().IsEqualTo(2);
        await Assert.That(entry.Lines.Sum(l => l.Amount)).IsEqualTo(0L);

        var bankLine = entry.Lines.Single(l => l.AccountId == fixture.OwnedAccountId);
        var groceriesLine = entry.Lines.Single(l => l.AccountId == groceries.Id);
        await Assert.That(bankLine.Amount).IsEqualTo(-4000L);
        await Assert.That(bankLine.ReconciliationStatus).IsEqualTo(ReconciliationStatus.Cleared);
        await Assert.That(groceriesLine.Amount).IsEqualTo(4000L);
        await Assert
            .That(groceriesLine.ReconciliationStatus)
            .IsEqualTo(ReconciliationStatus.Uncleared);
    }

    [Test]
    public async Task CategorizeAsync_with_new_counterparty_and_iban_creates_linked_bank_account(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var groceries = await fixture.CreateAccountAsync(
            "Groceries-newcp",
            AccountType.Expense,
            cancellationToken
        );

        var counterpartyIban = $"NL69INGB{NextDigits(10)}";
        var newName = $"AH-new-{Guid.NewGuid():N}";
        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -1234,
            counterpartyAccountNumber: counterpartyIban,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: new NewCounterpartyInput(newName),
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(groceries.Id, 1234, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var counterparty = await dbContext.Counterparties.SingleAsync(
            c => c.Name == newName,
            cancellationToken
        );
        var linkedBankAccount = await dbContext.BankAccounts.SingleAsync(
            b => b.Iban == counterpartyIban,
            cancellationToken
        );
        await Assert.That(linkedBankAccount.CounterpartyId).IsEqualTo(counterparty.Id);
        await Assert.That(linkedBankAccount.AccountId).IsNull();
        await Assert.That(linkedBankAccount.CurrencyCode).IsNull();
        await Assert.That(result.Value!.CounterpartyId).IsEqualTo(counterparty.Id);
    }

    [Test]
    public async Task CategorizeAsync_with_new_counterparty_and_no_iban_skips_bank_account_creation(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var fees = await fixture.CreateAccountAsync(
            "BankFees-cat",
            AccountType.Expense,
            cancellationToken
        );

        var newName = $"Bank-fee-cp-{Guid.NewGuid():N}";
        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -500,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: new NewCounterpartyInput(newName),
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(fees.Id, 500, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var counterparty = await dbContext.Counterparties.SingleAsync(
            c => c.Name == newName,
            cancellationToken
        );
        var bankAccountCount = await dbContext.BankAccounts.CountAsync(
            b => b.CounterpartyId == counterparty.Id,
            cancellationToken
        );
        await Assert.That(bankAccountCount).IsEqualTo(0);
    }

    [Test]
    public async Task CategorizeAsync_with_split_creates_multi_line_balanced_entry(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"AH-split-cat-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;
        var groceries = await fixture.CreateAccountAsync(
            "Groceries-split-cat",
            AccountType.Expense,
            cancellationToken
        );
        var household = await fixture.CreateAccountAsync(
            "Household-split-cat",
            AccountType.Expense,
            cancellationToken
        );

        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -8743,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: "AH split",
                Lines:
                [
                    new CategorizeBankTransactionLineInput(groceries.Id, 6000, null),
                    new CategorizeBankTransactionLineInput(household.Id, 2743, null),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Lines).Count().IsEqualTo(3);
        await Assert.That(result.Value!.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
    }

    [Test]
    public async Task CategorizeAsync_sum_mismatch_returns_invariant_error(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"mismatch-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;
        var expense = await fixture.CreateAccountAsync(
            "Mismatch-cat",
            AccountType.Expense,
            cancellationToken
        );

        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -1000,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 999, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
    }

    [Test]
    public async Task CategorizeAsync_already_categorised_returns_conflict(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"already-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;
        var expense = await fixture.CreateAccountAsync(
            "Already-cat",
            AccountType.Expense,
            cancellationToken
        );
        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -500,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var first = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 500, null)]
            ),
            cancellationToken
        );
        await Assert.That(first.IsSuccess).IsTrue();

        var second = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 500, null)]
            ),
            cancellationToken
        );

        await Assert.That(second.IsFailure).IsTrue();
        await Assert.That(second.Error).IsTypeOf<ConflictError>();
        var conflict = (ConflictError)second.Error!;
        await Assert.That(conflict.Code).IsEqualTo(ErrorCodes.BankTransactionAlreadyCategorised);
    }

    [Test]
    public async Task CategorizeAsync_dismissed_returns_invariant_error(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"dismissed-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;
        var expense = await fixture.CreateAccountAsync(
            "Dismissed-cat",
            AccountType.Expense,
            cancellationToken
        );
        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -500,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var dismissResult = await fixture.BankTransactionService.DismissAsync(
            bankTransaction.Id,
            "test dismissal",
            cancellationToken
        );
        await Assert.That(dismissResult.IsSuccess).IsTrue();

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 500, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var invariant = (InvariantError)result.Error!;
        await Assert.That(invariant.Code).IsEqualTo(ErrorCodes.BankTransactionDismissed);
    }

    [Test]
    public async Task CategorizeAsync_unknown_bank_transaction_returns_not_found(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"unknown-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;
        var expense = await fixture.CreateAccountAsync(
            "Unknown-cat",
            AccountType.Expense,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            new BankTransactionId(Guid.NewGuid()),
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: null,
                Date: new DateOnly(2026, 5, 1),
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 100, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<NotFoundError>();
    }

    [Test]
    public async Task CategorizeAsync_self_transfer_with_null_counterparty_creates_balanced_entry(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var savings = await fixture.CreateAccountAsync(
            "Savings-self-transfer",
            AccountType.Asset,
            cancellationToken
        );

        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -25000,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: "Transfer to savings",
                Lines: [new CategorizeBankTransactionLineInput(savings.Id, 25000, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var entry = result.Value!;
        await Assert.That(entry.BankTransactions).Count().IsEqualTo(1);
        await Assert.That(entry.BankTransactions[0].Id).IsEqualTo(bankTransaction.Id);
        await Assert.That(entry.BankTransactions[0].JournalEntryId).IsEqualTo(entry.Id);
        await Assert.That(entry.CounterpartyId).IsNull();
        await Assert.That(entry.Lines).Count().IsEqualTo(2);
        await Assert.That(entry.Lines.Sum(l => l.Amount)).IsEqualTo(0L);

        var bankLine = entry.Lines.Single(l => l.AccountId == fixture.OwnedAccountId);
        var savingsLine = entry.Lines.Single(l => l.AccountId == savings.Id);
        await Assert.That(bankLine.Amount).IsEqualTo(-25000L);
        await Assert.That(bankLine.ReconciliationStatus).IsEqualTo(ReconciliationStatus.Cleared);
        await Assert.That(savingsLine.Amount).IsEqualTo(25000L);
        await Assert
            .That(savingsLine.ReconciliationStatus)
            .IsEqualTo(ReconciliationStatus.Uncleared);
    }

    [Test]
    public async Task CategorizeAsync_both_counterparty_options_returns_invariant_error(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var counterparty = (
            await fixture.CounterpartyService.CreateAsync(
                $"both-{Guid.NewGuid():N}",
                cancellationToken
            )
        ).Value!;
        var expense = await fixture.CreateAccountAsync(
            "Both-cat",
            AccountType.Expense,
            cancellationToken
        );
        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -500,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorisationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: counterparty.Id,
                NewCounterparty: new NewCounterpartyInput("also-new"),
                Date: bankTransaction.BookingDate,
                Description: null,
                Lines: [new CategorizeBankTransactionLineInput(expense.Id, 500, null)]
            ),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var invariant = (InvariantError)result.Error!;
        await Assert.That(invariant.Code).IsEqualTo(ErrorCodes.CategoriseCounterpartySelection);
    }

    private async Task<Fixture> SeedAsync(CancellationToken cancellationToken)
    {
        var scope = Factory.Services.CreateAsyncScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var bankTransactionService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionService>();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var categorisationService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionCategorisationService>();

        var ownedAccount = (
            await accountService.CreateAsync(
                $"Checking-cat-{Guid.NewGuid():N}",
                AccountType.Asset,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;

        var ownedBankAccount = (
            await bankAccountService.CreateAsync(
                new CreateBankAccountInput(
                    Type: BankAccountType.Current,
                    Iban: $"NL69INGB{NextDigits(10)}",
                    AccountNumber: null,
                    CardIdentifier: null,
                    Bic: null,
                    BankName: null,
                    AccountHolderName: null,
                    CurrencyCode: new CurrencyCode("EUR"),
                    ImporterKey: null,
                    AccountId: ownedAccount.Id,
                    CounterpartyId: null
                ),
                cancellationToken
            )
        ).Value!;

        return new Fixture(
            scope,
            accountService,
            bankAccountService,
            bankTransactionService,
            counterpartyService,
            categorisationService,
            ownedAccount.Id,
            ownedBankAccount.Id
        );
    }

    private sealed record Fixture(
        AsyncServiceScope Scope,
        IAccountService AccountService,
        IBankAccountService BankAccountService,
        IBankTransactionService BankTransactionService,
        ICounterpartyService CounterpartyService,
        IBankTransactionCategorisationService CategorisationService,
        AccountId OwnedAccountId,
        BankAccountId OwnedBankAccountId
    ) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scope.DisposeAsync();

        public async Task<AccountOutput> CreateAccountAsync(
            string namePrefix,
            AccountType type,
            CancellationToken cancellationToken
        )
        {
            var result = await AccountService.CreateAsync(
                $"{namePrefix}-{Guid.NewGuid():N}",
                type,
                new CurrencyCode("EUR"),
                cancellationToken
            );
            return result.Value!;
        }

        public async Task<BankTransactionOutput> CreateBankTransactionAsync(
            long amount,
            string? counterpartyAccountNumber,
            CancellationToken cancellationToken
        )
        {
            var result = await BankTransactionService.CreateAsync(
                new CreateBankTransactionInput(
                    BankAccountId: OwnedBankAccountId,
                    BookingDate: new DateOnly(2026, 5, 17),
                    Amount: amount,
                    CurrencyCode: new CurrencyCode("EUR"),
                    Description: "cat-test",
                    CounterpartyName: "Counter test",
                    CounterpartyAccountNumber: counterpartyAccountNumber
                ),
                cancellationToken
            );
            return result.Value!;
        }
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
