using System.Text;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Integration.Rabobank.Importers;
using Balance.Integration.Rabobank.Parsers;
using Balance.Services.Contracts;

namespace Balance.Tests.Integrations.Rabobank;

internal sealed class RabobankAccountExtractorTests
{
    private const string CurrentExport = "CSV_A_NL12RABO0123456789_EUR_20260101_20260131.csv";
    private const string SavingsExport = "CSV_A_NL34RABO0987654321_EUR_20260101_20260131.csv";
    private const string CurrentIban = "NL12RABO0123456789";
    private const string SavingsIban = "NL34RABO0987654321";

    [Test]
    public async Task Maps_the_current_account_export(CancellationToken cancellationToken)
    {
        var rows = await ExtractAsync(CurrentExport, OwnedAccount(CurrentIban), cancellationToken);

        await Assert.That(rows.Count).IsEqualTo(7);
        await Assert.That(rows[0].BookingDate).IsEqualTo(new DateOnly(2026, 1, 2));
        await Assert.That(rows.All(row => row.ImporterKey == "Rabobank.CurrentAccount")).IsTrue();

        var directDebit = rows[1];
        await Assert.That(directDebit.Description).IsEqualTo("Voorschot januari 2026");
        await Assert.That(directDebit.CounterpartyName).IsEqualTo("Example Energy B.V.");
        await Assert.That(directDebit.CounterpartyAccountNumber).IsEqualTo("NL55TEST0000000001");
        await Assert.That(directDebit.Money.Amount).IsEqualTo(-4500L);
        await Assert.That(directDebit.ValueDate).IsEqualTo(new DateOnly(2026, 1, 5));
        await Assert.That(directDebit.MandateId).IsEqualTo("MANDATE-0001");
        await Assert.That(directDebit.SepaCreditorId).IsEqualTo("NL00ZZZ000000000001");
        await Assert.That(directDebit.Reference).IsNull();
        await Assert.That(Metadata(directDebit, "Rabobank Code")).IsEqualTo("ei");
        await Assert.That(Metadata(directDebit, "Rabobank Volgnr")).IsEqualTo("000000000000000002");
        await Assert
            .That(Metadata(directDebit, "Ultimate Party"))
            .IsEqualTo("Example Energy Retail");

        var salary = rows[^1];
        await Assert.That(salary.Money.Amount).IsEqualTo(250000L);
        await Assert
            .That(Metadata(salary, "Initiating Party"))
            .IsEqualTo("Example Payroll Services");
    }

    [Test]
    public async Task Describes_card_payments_by_the_merchant(CancellationToken cancellationToken)
    {
        var rows = await ExtractAsync(CurrentExport, OwnedAccount(CurrentIban), cancellationToken);

        var payment = rows[0];
        await Assert.That(payment.Description).IsEqualTo("BCK*Corner Bakery");
        await Assert.That(payment.CounterpartyName).IsEqualTo("BCK*Corner Bakery");
        await Assert.That(payment.CounterpartyAccountNumber).IsNull();
        await Assert
            .That(Metadata(payment, "Card Location"))
            .IsEqualTo("UTRECHT, 3511AA, NLD, 12:24");
        await Assert.That(Metadata(payment, "Card Terminal")).StartsWith("Pas: 1xxxx2345");

        var withdrawalAbroad = rows[4];
        await Assert.That(withdrawalAbroad.Description).IsEqualTo("EXAMPLE ATM");
        await Assert.That(withdrawalAbroad.Money.Amount).IsEqualTo(-7110L);
        await Assert.That(withdrawalAbroad.ForeignAmount).IsEqualTo(21_300_000L);
        await Assert.That(withdrawalAbroad.ForeignCurrencyCode).IsEqualTo("TZS");
        await Assert.That(withdrawalAbroad.ExchangeRate).IsEqualTo(3037.64974m);
    }

    [Test]
    public async Task Falls_back_to_the_counterparty_when_there_is_no_description(
        CancellationToken cancellationToken
    )
    {
        var rows = await ExtractAsync(CurrentExport, OwnedAccount(CurrentIban), cancellationToken);

        var transfer = rows[2];
        await Assert.That(transfer.Description).IsEqualTo("J. Doe");
        await Assert.That(transfer.CounterpartyAccountNumber).IsEqualTo(SavingsIban);
    }

    [Test]
    public async Task Does_not_treat_bank_fees_as_a_transfer_to_the_account_itself(
        CancellationToken cancellationToken
    )
    {
        var rows = await ExtractAsync(CurrentExport, OwnedAccount(CurrentIban), cancellationToken);

        var fee = rows[3];
        await Assert
            .That(fee.Description)
            .IsEqualTo("Kosten Rabo Standaard Periode 01-01-2026 t/m 31-01-2026");
        await Assert.That(fee.CounterpartyAccountNumber).IsNull();
    }

    [Test]
    public async Task Records_the_card_on_a_card_settlement(CancellationToken cancellationToken)
    {
        var rows = await ExtractAsync(CurrentExport, OwnedAccount(CurrentIban), cancellationToken);

        var settlement = rows[5];
        await Assert.That(settlement.CounterpartyAccountNumber).IsNull();
        await Assert.That(Metadata(settlement, "Card Identifier")).IsEqualTo("************1234");
    }

    [Test]
    public async Task Imports_a_savings_export_without_rows(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture(SavingsExport);
        var result = await new RabobankSavingsAccountTransactionExtractor(
            new RabobankAccountStatementParser()
        ).ExtractAsync(
            OwnedAccount(SavingsIban, BankAccountType.Savings),
            stream,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Identifies_the_account_from_the_filename(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture(SavingsExport);
        var identity = await new RabobankSavingsAccountTransactionExtractor(
            new RabobankAccountStatementParser()
        ).TryIdentifyAsync(new ImportFile(SavingsExport, stream), cancellationToken);

        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.ImporterKey).IsEqualTo("Rabobank.SavingsAccount");
        await Assert.That(identity.SupportedType).IsEqualTo(BankAccountType.Savings);
        await Assert.That(identity.AccountAnchor).IsEqualTo(SavingsIban);
    }

    [Test]
    public async Task Identifies_a_renamed_file_from_its_content(
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(CurrentExport);
        var identity = await BuildExtractor()
            .TryIdentifyAsync(new ImportFile("export.csv", stream), cancellationToken);

        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.AccountAnchor).IsEqualTo(CurrentIban);
        await Assert.That(stream.Position).IsEqualTo(0L);
    }

    [Test]
    public async Task Rejects_a_statement_for_another_account(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture(CurrentExport);
        var result = await BuildExtractor()
            .ExtractAsync(OwnedAccount(SavingsIban), stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportIbanMismatch);
    }

    [Test]
    public async Task Rejects_a_file_that_mixes_accounts(CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(
            FixturePath(CurrentExport),
            Encoding.Latin1,
            cancellationToken
        );
        var lastRow = content.LastIndexOf($"\"{CurrentIban}\"", StringComparison.Ordinal);
        var mixed = string.Concat(
            content[..lastRow],
            $"\"{SavingsIban}\"",
            content[(lastRow + CurrentIban.Length + 2)..]
        );
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(mixed));

        var result = await BuildExtractor()
            .ExtractAsync(OwnedAccount(CurrentIban), stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportAccountColumnDivergence);
    }

    [Test]
    public async Task Rejects_a_file_in_another_layout(CancellationToken cancellationToken)
    {
        await using var stream = OpenFixture("RA_CC_1234_20260101_20260131.csv");
        var result = await BuildExtractor()
            .ExtractAsync(OwnedAccount(CurrentIban), stream, cancellationToken);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Code).IsEqualTo(ErrorCodes.ImportFormatInvalid);
    }

    private static async Task<IReadOnlyList<BankTransaction>> ExtractAsync(
        string fileName,
        BankAccount bankAccount,
        CancellationToken cancellationToken
    )
    {
        await using var stream = OpenFixture(fileName);
        var result = await BuildExtractor().ExtractAsync(bankAccount, stream, cancellationToken);

        await Assert.That(result.IsSuccess).IsTrue();
        return result.Value!;
    }

    private static RabobankCurrentAccountTransactionExtractor BuildExtractor() =>
        new(new RabobankAccountStatementParser());

    private static BankAccount OwnedAccount(
        string iban,
        BankAccountType type = BankAccountType.Current
    ) =>
        new()
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Type = type,
            Iban = iban,
            CurrencyCode = new CurrencyCode("EUR"),
            AccountId = new AccountId(Guid.CreateVersion7()),
        };

    private static string? Metadata(BankTransaction row, string key) =>
        row
            .Metadata.Where(m => m.Key!.Name == key)
            .Select(m => m.StringValue ?? m.IntegerValue?.ToString(null as IFormatProvider))
            .FirstOrDefault();

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Integrations", "Rabobank", fileName);

    private static FileStream OpenFixture(string fileName) => File.OpenRead(FixturePath(fileName));
}
