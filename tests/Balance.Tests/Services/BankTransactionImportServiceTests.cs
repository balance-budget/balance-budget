using System.Security.Cryptography;
using System.Text;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class BankTransactionImportServiceTests : EndpointsTestsBase
{
    private const string Header =
        "\"Date\";\"Name / Description\";\"Account\";\"Counterparty\";\"Code\";"
        + "\"Debit/credit\";\"Amount (EUR)\";\"Transaction type\";\"Notifications\";"
        + "\"Resulting balance\";\"Tag\"";

    private const string Iban = "NL69INGB0123456789";

    private const string RowEtos =
        "\"20260131\";\"Etos\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
        + "\"72,30\";\"Payment terminal\";\"Card sequence no.: 001 31-01-2026 09:26\";\"1183,44\";\"\"";

    private const string RowCoolblue =
        "\"20260130\";\"Coolblue BV\";\"NL69INGB0123456789\";\"NL22INGB3141592653\";\"GT\";"
        + "\"Credit\";\"59,95\";\"Online Banking\";\"Name: Coolblue BV\";\"1412,95\";\"\"";

    private const string RowAh =
        "\"20260129\";\"Albert Heijn\";\"NL69INGB0123456789\";\"\";\"BA\";\"Debit\";"
        + "\"21,40\";\"Payment terminal\";\"Card sequence no.: 002 30-01-2026 14:12\";\"1232,90\";\"\"";

    private static MemoryStream CsvStream(params string[] rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var row in rows)
            sb.AppendLine(row);
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Test]
    public async Task ImportAsync_persists_extracted_rows_on_happy_path(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL69INGB{NextDigits(10)}",
            cancellationToken
        );

        await using var stream = CsvStreamFor(bankAccount.Iban!, RowEtos, RowCoolblue);
        var result = await importService.ImportAsync(bankAccount.Id, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Imported).IsEqualTo(2);
        await Assert.That(result.Value!.SkippedAsDuplicate).IsEqualTo(0);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var persisted = await db
            .BankTransactions.AsNoTracking()
            .Where(b => b.BankAccountId == bankAccount.Id)
            .ToListAsync(cancellationToken);
        await Assert.That(persisted.Count).IsEqualTo(2);
        await Assert.That(persisted.All(b => b.CreatedAt != default)).IsTrue();
        await Assert.That(persisted.All(b => b.RowHash.Length == 64)).IsTrue();
    }

    [Test]
    public async Task ImportAsync_is_idempotent_on_re_upload(CancellationToken cancellationToken)
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL69INGB{NextDigits(10)}",
            cancellationToken
        );

        await using (var firstStream = CsvStreamFor(bankAccount.Iban!, RowEtos, RowCoolblue))
        {
            var firstResult = await importService.ImportAsync(
                bankAccount.Id,
                firstStream,
                cancellationToken
            );
            await Assert.That(firstResult.IsSuccess).IsTrue();
            await Assert.That(firstResult.Value!.Imported).IsEqualTo(2);
        }

        await using var secondStream = CsvStreamFor(bankAccount.Iban!, RowEtos, RowCoolblue);
        var secondResult = await importService.ImportAsync(
            bankAccount.Id,
            secondStream,
            cancellationToken
        );

        await Assert.That(secondResult.IsSuccess).IsTrue();
        await Assert.That(secondResult.Value!.Imported).IsEqualTo(0);
        await Assert.That(secondResult.Value!.SkippedAsDuplicate).IsEqualTo(2);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await db.BankTransactions.CountAsync(
            b => b.BankAccountId == bankAccount.Id,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ImportAsync_partitions_overlapping_uploads(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL69INGB{NextDigits(10)}",
            cancellationToken
        );

        await using (var firstStream = CsvStreamFor(bankAccount.Iban!, RowEtos, RowCoolblue))
        {
            var firstResult = await importService.ImportAsync(
                bankAccount.Id,
                firstStream,
                cancellationToken
            );
            await Assert.That(firstResult.IsSuccess).IsTrue();
            await Assert.That(firstResult.Value!.Imported).IsEqualTo(2);
        }

        await using var secondStream = CsvStreamFor(bankAccount.Iban!, RowCoolblue, RowAh);
        var secondResult = await importService.ImportAsync(
            bankAccount.Id,
            secondStream,
            cancellationToken
        );

        await Assert.That(secondResult.IsSuccess).IsTrue();
        await Assert.That(secondResult.Value!.Imported).IsEqualTo(1);
        await Assert.That(secondResult.Value!.SkippedAsDuplicate).IsEqualTo(1);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await db.BankTransactions.CountAsync(
            b => b.BankAccountId == bankAccount.Id,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task ImportAsync_collapses_in_batch_duplicate_rows(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL69INGB{NextDigits(10)}",
            cancellationToken
        );

        await using var stream = CsvStreamFor(bankAccount.Iban!, RowEtos, RowEtos);
        var result = await importService.ImportAsync(bankAccount.Id, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Imported).IsEqualTo(1);
        await Assert.That(result.Value!.SkippedAsDuplicate).IsEqualTo(1);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await db.BankTransactions.CountAsync(
            b => b.BankAccountId == bankAccount.Id,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task ImportAsync_returns_NotFound_for_unknown_bank_account(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();

        await using var stream = CsvStreamFor(Iban, RowEtos);
        var result = await importService.ImportAsync(
            new BankAccountId(Guid.NewGuid()),
            stream,
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<NotFoundError>();
    }

    [Test]
    public async Task ImportAsync_rejects_counterparty_owned_bank_account(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateCounterpartyBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL01INGB{NextDigits(10)}",
            cancellationToken
        );

        await using var stream = CsvStreamFor(bankAccount.Iban!, RowEtos);
        var result = await importService.ImportAsync(bankAccount.Id, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportBankAccountNotOwned);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await db.BankTransactions.CountAsync(
            b => b.BankAccountId == bankAccount.Id,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ImportAsync_rejects_when_csv_account_does_not_match(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL02INGB{NextDigits(10)}",
            cancellationToken
        );

        await using var stream = CsvStreamFor("NL11RABO9999999999", RowEtos);
        var result = await importService.ImportAsync(bankAccount.Id, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportIbanMismatch);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await db.BankTransactions.CountAsync(
            b => b.BankAccountId == bankAccount.Id,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ImportAsync_rejects_malformed_csv_and_persists_nothing(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL03INGB{NextDigits(10)}",
            cancellationToken
        );

        var malformedRow =
            $"\"20260131\";\"Etos\";\"{bankAccount.Iban}\";\"\";\"BA\";\"Debit\";"
            + "\"not-a-number\";\"Payment terminal\";\"x\";\"100,00\";\"\"";
        await using var stream = CsvStream(malformedRow);
        var result = await importService.ImportAsync(bankAccount.Id, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportFormatInvalid);

        using var verifyScope = Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var count = await db.BankTransactions.CountAsync(
            b => b.BankAccountId == bankAccount.Id,
            cancellationToken
        );
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ImportAsync_with_empty_csv_returns_zero_counts(
        CancellationToken cancellationToken
    )
    {
        using var scope = Factory.Services.CreateScope();
        var importService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionImportService>();
        var bankAccount = await CreateOwnedBankAccountAsync(
            scope.ServiceProvider,
            iban: $"NL04INGB{NextDigits(10)}",
            cancellationToken
        );

        await using var stream = CsvStream();
        var result = await importService.ImportAsync(bankAccount.Id, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Imported).IsEqualTo(0);
        await Assert.That(result.Value!.SkippedAsDuplicate).IsEqualTo(0);
    }

    private static MemoryStream CsvStreamFor(string accountInCsv, params string[] templateRows)
    {
        var swapped = templateRows
            .Select(r => r.Replace(Iban, accountInCsv, StringComparison.Ordinal))
            .ToArray();
        return CsvStream(swapped);
    }

    private static async Task<BankAccount> CreateOwnedBankAccountAsync(
        IServiceProvider serviceProvider,
        string iban,
        CancellationToken cancellationToken
    )
    {
        var accountService = serviceProvider.GetRequiredService<IAccountService>();
        var bankAccountService = serviceProvider.GetRequiredService<IBankAccountService>();

        var accountResult = await accountService.CreateAsync(
            $"Checking-{Guid.NewGuid():N}",
            AccountType.Asset,
            new CurrencyCode("EUR"),
            cancellationToken
        );
        await Assert.That(accountResult.IsSuccess).IsTrue();

        var bankAccountResult = await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Type: BankAccountType.Current,
                Iban: iban,
                AccountNumber: null,
                CardIdentifier: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: new CurrencyCode("EUR"),
                ImporterKey: "Ing.CurrentAccount",
                AccountId: accountResult.Value!.Id,
                CounterpartyId: null
            ),
            cancellationToken
        );
        await Assert.That(bankAccountResult.IsSuccess).IsTrue();

        var db = serviceProvider.GetRequiredService<BalanceDbContext>();
        return await db
            .BankAccounts.AsNoTracking()
            .FirstAsync(b => b.Id == bankAccountResult.Value!.Id, cancellationToken);
    }

    private static async Task<BankAccount> CreateCounterpartyBankAccountAsync(
        IServiceProvider serviceProvider,
        string iban,
        CancellationToken cancellationToken
    )
    {
        var counterpartyService = serviceProvider.GetRequiredService<ICounterpartyService>();
        var bankAccountService = serviceProvider.GetRequiredService<IBankAccountService>();

        var counterpartyResult = await counterpartyService.CreateAsync(
            $"CP-{Guid.NewGuid():N}",
            cancellationToken
        );
        await Assert.That(counterpartyResult.IsSuccess).IsTrue();

        var bankAccountResult = await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Type: BankAccountType.Current,
                Iban: iban,
                AccountNumber: null,
                CardIdentifier: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: null,
                ImporterKey: null,
                AccountId: null,
                CounterpartyId: counterpartyResult.Value!.Id
            ),
            cancellationToken
        );
        await Assert.That(bankAccountResult.IsSuccess).IsTrue();

        // Force-stamp ImporterKey on this counterparty-owned BankAccount so the dispatcher hands
        // off to the extractor; that's the layer the rejects_counterparty_owned_bank_account test
        // exercises (extractor enforces AccountId IS NOT NULL).
        var db = serviceProvider.GetRequiredService<BalanceDbContext>();
        var row = await db.BankAccounts.FirstAsync(
            b => b.Id == bankAccountResult.Value!.Id,
            cancellationToken
        );
        row.ImporterKey = "Ing.CurrentAccount";
        await db.SaveChangesAsync(cancellationToken);
        return await db
            .BankAccounts.AsNoTracking()
            .FirstAsync(b => b.Id == bankAccountResult.Value!.Id, cancellationToken);
    }

    private static string NextDigits(int length)
    {
        var digits = new char[length];
        for (var i = 0; i < length; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        return new string(digits);
    }
}
