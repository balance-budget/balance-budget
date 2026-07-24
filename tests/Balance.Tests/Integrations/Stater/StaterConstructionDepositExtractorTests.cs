using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Integration.Stater.Importers;
using Balance.Integration.Stater.Parsers;
using Balance.Services.Contracts;

namespace Balance.Tests.Integrations.Stater;

internal sealed class StaterConstructionDepositExtractorTests
{
    private static StaterConstructionDepositExtractor BuildExtractor() =>
        new(new StaterStatementParser());

    private static BankAccount OwnedSavingsAccount(string accountNumber = "1234567890") =>
        new()
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Iban = null,
            AccountNumber = accountNumber,
            CurrencyCode = new CurrencyCode("EUR"),
            AccountId = new AccountId(Guid.CreateVersion7()),
            CounterpartyId = null,
        };

    [Test]
    public async Task Rejects_when_bank_account_is_not_owned(CancellationToken cancellationToken)
    {
        var bankAccount = new BankAccount
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            AccountNumber = "1234567890",
            CurrencyCode = new CurrencyCode("EUR"),
            AccountId = null,
        };
        await using var stream = new MemoryStream("%PDF-1.4 not a real pdf"u8.ToArray());

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportBankAccountNotOwned);
    }

    [Test]
    public async Task Rejects_when_currency_is_not_EUR(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedSavingsAccount();
        bankAccount.CurrencyCode = new CurrencyCode("USD");
        await using var stream = new MemoryStream("%PDF-1.4 not a real pdf"u8.ToArray());

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportCurrencyMismatch);
    }

    [Test]
    public async Task Rejects_unreadable_pdf(CancellationToken cancellationToken)
    {
        var bankAccount = OwnedSavingsAccount();
        await using var stream = new MemoryStream("not a pdf at all"u8.ToArray());

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportFormatInvalid);
    }

    [Test]
    public async Task TryIdentify_skips_non_pdf_content(CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream("not a pdf at all"u8.ToArray());
        var file = new ImportFile("statement.pdf", stream);

        var identity = await BuildExtractor().TryIdentifyAsync(file, cancellationToken);

        await Assert.That(identity).IsNull();
    }

    // Enable once a real Stater bouwdepot statement is placed alongside the test binary. Mirrors
    // the ING credit-card PDF tests, which are likewise gated on a private fixture.
    [Test]
    [Skip("Requires a real Stater statement PDF fixture")]
    public async Task Extracts_from_a_real_statement(CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Integrations",
            "Stater",
            "construction-deposit.pdf"
        );
        var bankAccount = OwnedSavingsAccount();
        await using var stream = File.OpenRead(path);

        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Count).IsGreaterThan(0);
        await Assert
            .That(result.Value!.All(r => r.ImporterKey == "Stater.ConstructionDeposit"))
            .IsTrue();
        await Assert.That(result.Value!.All(r => r.CounterpartyAccountNumber is null)).IsTrue();
    }
}
