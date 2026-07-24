using Balance.Integration.Stater.Models;
using Balance.Integration.Stater.Parsers;

namespace Balance.Tests.Integrations.Stater;

// Exercises the column-band reconstruction over synthetic positioned lines (no real statement PDF
// in the repo). Column x-anchors mirror the statement's Datum | Bedrag | Betaald aan | Omschrijving
// layout; the literal Dutch tokens are strings the statement itself carries.
internal sealed class StaterStatementParserTests
{
    // Column centers (arbitrary but ordered): Datum, Bedrag, Betaald aan, Omschrijving.
    private const double DatumX = 50;
    private const double BedragX = 150;
    private const double BetaaldX = 260;
    private const double OmschrijvingX = 400;

    private static StaterStatementParser BuildParser() => new();

    // A word placed near a column center, wide enough to read as that column.
    private static StaterWord At(double center, string text) => new(text, center - 5, center + 5);

    private static StaterTextLine Line(params StaterWord[] words) =>
        new(string.Join(' ', words.Select(w => w.Text)), words);

    private static StaterTextLine PlainLine(string text) => new(text, []);

    // The header wraps in the real statement ("afschrijving" lands on the next visual line), so the
    // anchor line reads "Datum bij– of Bedrag Betaald aan Omschrijving".
    private static StaterTextLine HeaderLine() =>
        Line(
            At(DatumX, "Datum"),
            At(DatumX + 12, "bij\u2013"),
            At(DatumX + 24, "of"),
            At(BedragX, "Bedrag"),
            At(BetaaldX, "Betaald"),
            At(BetaaldX + 20, "aan"),
            At(OmschrijvingX, "Omschrijving")
        );

    private static List<StaterTextLine> WithFrame(params StaterTextLine[] rows)
    {
        var lines = new List<StaterTextLine>
        {
            PlainLine("Rekeningoverzicht bouwdepot"),
            PlainLine("Nummer: 12.34.56"),
            HeaderLine(),
        };
        lines.AddRange(rows);
        lines.Add(PlainLine("Heeft u nog vragen? Bel ons."));
        return lines;
    }

    [Test]
    public async Task Signs_deposit_interest_credit_positive_and_settlement_negative()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "01-07-2026"),
                At(BedragX, "2.581,84"),
                At(OmschrijvingX, "Rentevergoeding")
            ),
            Line(
                At(DatumX, "01-07-2026"),
                At(BedragX, "-2.614,77"),
                At(BetaaldX, "Blabla"),
                At(BetaaldX + 30, "Bank"),
                At(OmschrijvingX, "Verrekening")
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(2);
        await Assert.That(statement.Rows[0].AmountMinorUnits).IsEqualTo(258184L);
        await Assert.That(statement.Rows[1].AmountMinorUnits).IsEqualTo(-261477L);
    }

    [Test]
    public async Task Merges_a_wrapped_description_cell_into_the_row_above()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "01-07-2026"),
                At(BedragX, "-2.614,77"),
                At(BetaaldX, "Blabla"),
                At(BetaaldX + 30, "Bank"),
                At(OmschrijvingX, "Verrekening"),
                At(OmschrijvingX + 40, "i.v.m."),
                At(OmschrijvingX + 80, "maandbedrag/")
            ),
            // Wrapped continuation: no date in the Datum column.
            Line(At(OmschrijvingX, "aflossing"))
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
        var row = statement.Rows[0];
        await Assert.That(row.CounterpartyName).IsEqualTo("Blabla Bank");
        // The '/' wrap rejoins without a space.
        await Assert.That(row.Description).IsEqualTo("Verrekening i.v.m. maandbedrag/aflossing");
    }

    [Test]
    public async Task Splits_counterparty_and_description_by_column_position()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "15-07-2026"),
                At(BedragX, "-5.000,00"),
                At(BetaaldX, "Aannemersbedrijf"),
                At(BetaaldX + 40, "De"),
                At(BetaaldX + 60, "Vries"),
                At(OmschrijvingX, "Uitbetaling")
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        var row = statement!.Rows[0];
        await Assert.That(row.CounterpartyName).IsEqualTo("Aannemersbedrijf De Vries");
        await Assert.That(row.Description).IsEqualTo("Uitbetaling");
    }

    [Test]
    public async Task Leaves_counterparty_null_when_the_column_is_empty()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "01-07-2026"),
                At(BedragX, "2.581,84"),
                At(OmschrijvingX, "Rentevergoeding")
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        var row = statement!.Rows[0];
        await Assert.That(row.Description).IsEqualTo("Rentevergoeding");
        await Assert.That(row.CounterpartyName).IsNull();
    }

    [Test]
    public async Task Parses_dutch_number_format_with_thousands_separator()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "15-07-2026"),
                At(BedragX, "-12.345,67"),
                At(OmschrijvingX, "Uitbetaling")
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows[0].AmountMinorUnits).IsEqualTo(-1234567L);
    }

    [Test]
    public async Task Reads_dates_in_day_month_year_order()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "07-06-2026"),
                At(BedragX, "1,00"),
                At(OmschrijvingX, "Rentevergoeding")
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows[0].Date).IsEqualTo(new DateOnly(2026, 6, 7));
    }

    [Test]
    public async Task Anchors_on_the_header_account_number()
    {
        var statement = BuildParser().Parse(WithFrame());

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.AccountNumber).IsEqualTo("12.34.56");
    }

    [Test]
    public async Task Returns_null_when_no_header_account_number_present()
    {
        var lines = new List<StaterTextLine>
        {
            HeaderLine(),
            Line(
                At(DatumX, "01-07-2026"),
                At(BedragX, "1,00"),
                At(OmschrijvingX, "Rentevergoeding")
            ),
        };

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNull();
    }

    [Test]
    public async Task Stops_at_the_closing_section()
    {
        var lines = WithFrame(
            Line(
                At(DatumX, "01-07-2026"),
                At(BedragX, "1,00"),
                At(OmschrijvingX, "Rentevergoeding")
            )
        );
        lines.Add(
            Line(At(DatumX, "09-07-2026"), At(BedragX, "9,99"), At(OmschrijvingX, "Naschrift"))
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        // The row after "Heeft u nog vragen?" is past the table and ignored.
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
    }
}
