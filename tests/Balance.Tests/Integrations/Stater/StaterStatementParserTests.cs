using Balance.Integration.Pdf;
using Balance.Integration.Stater.Parsers;

namespace Balance.Tests.Integrations.Stater;

// Exercises the column-band reconstruction over synthetic positioned lines; the real statement is
// covered end-to-end by StaterConstructionDepositStatementParserTests. Column x-anchors mirror the
// statement's Datum | Bedrag | Betaald aan | Omschrijving layout; the literal Dutch tokens are
// strings the statement itself carries.
internal sealed class StaterStatementParserTests
{
    // Column centers (arbitrary but ordered): Datum, Bedrag, Betaald aan, Omschrijving.
    private const double DatumX = 50;
    private const double BedragX = 150;
    private const double BetaaldX = 260;
    private const double OmschrijvingX = 400;

    private const double WordHeight = 10;
    private const double Leading = 12;

    private static StaterStatementParser BuildParser() => new();

    // A word placed near a column center, wide enough to read as that column.
    private static PdfWord At(double center, string text) =>
        new(text, center - 5, center + 5, WordHeight, 0);

    private static PdfWord[] Words(params PdfWord[] words) => words;

    private static PdfWord[] Plain(string text) => [At(DatumX, text)];

    // Lays the given lines out top-down one leading apart, which is what makes a line with no date
    // read as a wrapped cell of the row above it.
    private static List<PdfTextLine> Layout(params PdfWord[][] lines)
    {
        var laidOut = new List<PdfTextLine>(lines.Length);
        var baseline = lines.Length * Leading;

        foreach (var words in lines)
        {
            var placed = words
                .Select(w => new PdfWord(w.Text, w.Left, w.Right, baseline + WordHeight, baseline))
                .ToList();
            laidOut.Add(
                new PdfTextLine(1, baseline, string.Join(' ', placed.Select(w => w.Text)), placed)
            );
            baseline -= Leading;
        }

        return laidOut;
    }

    // The header wraps in the real statement ("afschrijving" lands on the next visual line), so the
    // anchor line reads "Datum bij– of Bedrag Betaald aan Omschrijving".
    private static PdfWord[] HeaderLine() =>
        Words(
            At(DatumX, "Datum"),
            At(DatumX + 12, "bij\u2013"),
            At(DatumX + 24, "of"),
            At(BedragX, "Bedrag"),
            At(BetaaldX, "Betaald"),
            At(BetaaldX + 20, "aan"),
            At(OmschrijvingX, "Omschrijving")
        );

    private static List<PdfTextLine> WithFrame(params PdfWord[][] rows) =>
        Layout([
            Plain("Rekeningoverzicht bouwdepot"),
            Plain("Nummer: 12.34.56"),
            HeaderLine(),
            .. rows,
            Plain("Heeft u nog vragen? Bel ons."),
        ]);

    [Test]
    public async Task Signs_deposit_interest_credit_positive_and_settlement_negative()
    {
        var lines = WithFrame(
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "2.581,84"),
                At(OmschrijvingX, "Rentevergoeding")
            ),
            Words(
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
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "-2.614,77"),
                At(BetaaldX, "Blabla"),
                At(BetaaldX + 30, "Bank"),
                At(OmschrijvingX, "Verrekening"),
                At(OmschrijvingX + 40, "i.v.m."),
                At(OmschrijvingX + 80, "maandbedrag/")
            ),
            // Wrapped continuation: no date in the Datum column.
            Words(At(OmschrijvingX, "aflossing"))
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
            Words(
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
            Words(
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
    public async Task Never_fills_a_column_left_empty_by_the_rows_first_line()
    {
        var lines = WithFrame(
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "2.581,84"),
                // No Betaald aan on the first line; the description wraps to a second line.
                At(OmschrijvingX, "Rentevergoeding")
            ),
            // Wrapped continuation whose word drifts into the (empty) Betaald aan band; it must
            // not be adopted as the counterparty.
            Words(At(BetaaldX, "juni"))
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
        var row = statement.Rows[0];
        await Assert.That(row.CounterpartyName).IsNull();
        await Assert.That(row.Description).IsEqualTo("Rentevergoeding");
    }

    [Test]
    public async Task Ignores_text_that_sits_a_block_below_the_last_row()
    {
        var lines = WithFrame(
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "2.581,84"),
                At(OmschrijvingX, "Rentevergoeding")
            )
        );

        // A page footer: same column bands as the table, but far enough below the last row that it
        // cannot be a wrapped cell of it.
        var footerBaseline = lines[^1].Baseline - (10 * Leading);
        lines.Add(
            new PdfTextLine(
                1,
                footerBaseline,
                "De Nederlandse vestiging is ingeschreven bij de KvK",
                [
                    new PdfWord(
                        "De",
                        DatumX,
                        DatumX + 10,
                        footerBaseline + WordHeight,
                        footerBaseline
                    ),
                    new PdfWord(
                        "Nederlandse",
                        BedragX,
                        BedragX + 40,
                        footerBaseline + WordHeight,
                        footerBaseline
                    ),
                ]
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
        await Assert.That(statement.Rows[0].Date).IsEqualTo(new DateOnly(2026, 7, 1));
        await Assert.That(statement.Rows[0].AmountMinorUnits).IsEqualTo(258184L);
    }

    [Test]
    public async Task Ignores_page_furniture_left_of_the_table()
    {
        var lines = WithFrame(
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "2.581,84"),
                At(OmschrijvingX, "Rentevergoeding")
            ),
            // Stater prints a rotated form code down the left margin, one glyph per visual line.
            [new PdfWord("0", 5, 8, WordHeight, 0)]
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
        await Assert.That(statement.Rows[0].Description).IsEqualTo("Rentevergoeding");
    }

    [Test]
    public async Task Parses_dutch_number_format_with_thousands_separator()
    {
        var lines = WithFrame(
            Words(
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
            Words(
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
        var lines = Layout(
            HeaderLine(),
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "1,00"),
                At(OmschrijvingX, "Rentevergoeding")
            )
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNull();
    }

    [Test]
    public async Task Stops_at_the_closing_section()
    {
        var lines = Layout(
            Plain("Nummer: 12.34.56"),
            HeaderLine(),
            Words(
                At(DatumX, "01-07-2026"),
                At(BedragX, "1,00"),
                At(OmschrijvingX, "Rentevergoeding")
            ),
            Plain("Heeft u nog vragen? Bel ons."),
            Words(At(DatumX, "09-07-2026"), At(BedragX, "9,99"), At(OmschrijvingX, "Naschrift"))
        );

        var statement = BuildParser().Parse(lines);

        await Assert.That(statement).IsNotNull();
        // The row after "Heeft u nog vragen?" is past the table and ignored.
        await Assert.That(statement!.Rows.Count).IsEqualTo(1);
    }
}
