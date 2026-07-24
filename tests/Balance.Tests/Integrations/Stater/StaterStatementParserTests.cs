using Balance.Integration.Stater.Parsers;

namespace Balance.Tests.Integrations.Stater;

// Exercises the provisional line grammar over canned text lines (no real statement PDF in the
// repo). The literal Dutch tokens ("Bouwdepotnummer", "Af"/"Bij", "Betaald aan") are the strings
// the statement itself carries.
internal sealed class StaterStatementParserTests
{
    private static readonly string[] Header = ["Bouwdepotnummer: 1234567890"];

    private static StaterStatementParser BuildParser() => new();

    [Test]
    public async Task Signs_deposit_interest_credit_positive_and_settlement_negative()
    {
        var lines = new List<string>(Header)
        {
            "01-06-2026 Rentevergoeding 123,45 Bij",
            "01-06-2026 Verrekening i.v.m. maandbedrag 100,00 Af",
        };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(2);
        await Assert.That(statement.Rows[0].AmountMinorUnits).IsEqualTo(12345L);
        await Assert.That(statement.Rows[1].AmountMinorUnits).IsEqualTo(-10000L);
    }

    [Test]
    public async Task Parses_dutch_number_format_with_thousands_separator()
    {
        var lines = new List<string>(Header) { "15-06-2026 Uitbetaling 12.345,67 Af" };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows[0].AmountMinorUnits).IsEqualTo(-1234567L);
    }

    [Test]
    public async Task Maps_description_and_counterparty_from_paid_to_marker()
    {
        var lines = new List<string>(Header)
        {
            "15-06-2026 Uitbetaling Betaald aan Aannemersbedrijf De Vries 5.000,00 Af",
        };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        var row = statement!.Rows[0];
        await Assert.That(row.Description).IsEqualTo("Uitbetaling");
        await Assert.That(row.CounterpartyName).IsEqualTo("Aannemersbedrijf De Vries");
    }

    [Test]
    public async Task Leaves_counterparty_null_when_no_paid_to_marker()
    {
        var lines = new List<string>(Header) { "01-06-2026 Rentevergoeding 123,45 Bij" };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        var row = statement!.Rows[0];
        await Assert.That(row.Description).IsEqualTo("Rentevergoeding");
        await Assert.That(row.CounterpartyName).IsNull();
    }

    [Test]
    public async Task Reads_dates_in_day_month_year_order()
    {
        var lines = new List<string>(Header) { "07-06-2026 Rentevergoeding 1,00 Bij" };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows[0].Date).IsEqualTo(new DateOnly(2026, 6, 7));
    }

    [Test]
    public async Task Anchors_on_the_header_account_number()
    {
        var lines = new List<string>(Header) { "01-06-2026 Rentevergoeding 1,00 Bij" };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.AccountNumber).IsEqualTo("1234567890");
    }

    [Test]
    public async Task Returns_null_when_no_header_account_number_present()
    {
        var lines = new List<string> { "01-06-2026 Rentevergoeding 1,00 Bij" };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNull();
    }

    [Test]
    public async Task Ignores_non_transaction_lines()
    {
        var lines = new List<string>(Header)
        {
            "Rekeningoverzicht bouwdepot",
            "Datum Omschrijving Bedrag",
            "01-06-2026 Rentevergoeding 1,00 Bij",
            "Eindsaldo 10.000,00",
        };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Preserves_raw_record_for_idempotent_reimport()
    {
        const string RawRow = "01-06-2026 Rentevergoeding 123,45 Bij";
        var lines = new List<string>(Header) { RawRow };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows[0].RawRecord).IsEqualTo(RawRow);
    }
}
