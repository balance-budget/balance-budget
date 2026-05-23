using Balance.Integration.Ing.Parsers;

namespace Balance.Tests.Integrations.Ing;

internal sealed class IngNoteParserTests
{
    private readonly IngNoteParser _parser = new();

    [Test]
    [Arguments(
        "Pasvolgnr: 008 12-01-2019 15:26 Transactie: 17L9C5 Term: CT620773 Valutadatum: 14-01-2019"
    )]
    [Arguments(
        "Card sequence no.: 008 12/01/2019 15:26 Transaction: 17L9C5 Term: CT620773 Value date: 14/01/2019"
    )]
    public async Task ParsesCardPayment(string note)
    {
        var result = _parser.ParseNote(note);

        await Assert.That(result.CardSequence).IsNotNull();
        await Assert.That(result.CardSequence.SequenceNumber).IsEqualTo("008");
        await Assert
            .That(result.CardSequence.DateTime)
            .IsEqualTo(new DateTime(2019, 1, 12, 15, 26, 0));

        await Assert.That(result.Transaction).IsEqualTo("17L9C5");
        await Assert.That(result.Term).IsEqualTo("CT620773");

        await Assert.That(result.ValueDate).IsEqualTo(new DateOnly(2019, 1, 14));
    }

    [Test]
    [Arguments(
        "Pasvolgnr: 008 30-12-2016 14:20 Transactie: Q71243 Term: ATM12713 Valuta: 100,00 BYN Koers: 2,4406776 Opslag: 0,45 EUR Kosten: 2,25 EUR Valutadatum: 31-12-2016"
    )]
    [Arguments(
        "Card sequence no.: 008 30/12/2016 14:20 Transaction: Q71243 Term: ATM12713 Currency: 100,00 BYN Rate: 2,4406776 Mark-up: 0,45 EUR Fee: 2,25 EUR Value date: 31/12/2016"
    )]
    public async Task ParsesForeignCurrencyWithdrawal(string note)
    {
        var result = _parser.ParseNote(note);

        await Assert.That(result.CardSequence).IsNotNull();
        await Assert.That(result.CardSequence.SequenceNumber).IsEqualTo("008");
        await Assert
            .That(result.CardSequence.DateTime)
            .IsEqualTo(new DateTime(2016, 12, 30, 14, 20, 0));

        await Assert.That(result.Transaction).IsEqualTo("Q71243");
        await Assert.That(result.Term).IsEqualTo("ATM12713");

        await Assert.That(result.ForeignCurrencyAmount).IsNotNull();
        await Assert.That(result.ForeignCurrencyAmount.Amount).IsEqualTo(100.00m);
        await Assert.That(result.ForeignCurrencyAmount.CurrencyCode).IsEqualTo("BYN");

        await Assert.That(result.ForeignCurrencyRate).IsEqualTo(2.4406776m);

        await Assert.That(result.ForeignCurrencyMarkUp).IsNotNull();
        await Assert.That(result.ForeignCurrencyMarkUp.Amount).IsEqualTo(0.45m);
        await Assert.That(result.ForeignCurrencyMarkUp.CurrencyCode).IsEqualTo("EUR");

        await Assert.That(result.ForeignCurrencyFee).IsNotNull();
        await Assert.That(result.ForeignCurrencyFee.Amount).IsEqualTo(2.25m);
        await Assert.That(result.ForeignCurrencyFee.CurrencyCode).IsEqualTo("EUR");

        await Assert.That(result.ValueDate).IsEqualTo(new DateOnly(2016, 12, 31));
    }

    [Test]
    [Arguments(
        "Naam: SPOTIFY BY ADYEN Omschrijving: SpotifyNL P0102A103D IBAN: NL48ABNA0502830042 Kenmerk: D1815340503797720C Machtiging ID: 4815225945787361 Incassant ID: NL74ZZZ546978200017 Doorlopende incasso Overige partij: SpotifyNL Valutadatum: 14-08-2018"
    )]
    [Arguments(
        "Name: SPOTIFY BY ADYEN Description: SpotifyNL P0102A103D IBAN: NL48ABNA0502830042 Reference: D1815340503797720C Mandate ID: 4815225945787361 Creditor ID: NL74ZZZ546978200017 Recurrent SEPA direct debit Other party: SpotifyNL Value date: 14/08/2018"
    )]
    public async Task ParsesSepaDirectDebit(string note)
    {
        var result = _parser.ParseNote(note);

        await Assert.That(result.Name).IsEqualTo("SPOTIFY BY ADYEN");
        await Assert.That(result.Description).IsEqualTo("SpotifyNL P0102A103D");
        await Assert.That(result.Iban).IsEqualTo("NL48ABNA0502830042");
        await Assert.That(result.Reference).IsEqualTo("D1815340503797720C");
        await Assert.That(result.MandateId).IsEqualTo("4815225945787361");
        await Assert.That(result.Creditor).IsNotNull();
        await Assert.That(result.Creditor.Id).IsEqualTo("NL74ZZZ546978200017");
        await Assert.That(result.OtherParty).IsEqualTo("SpotifyNL");

        await Assert.That(result.ValueDate).IsEqualTo(new DateOnly(2018, 8, 14));
    }

    [Test]
    [Arguments(
        "Naam: SomeCompany B.V. Omschrijving: Payment IBAN: EE406224654075974290 Kenmerk: df1c6177f0ed4e81a2dea3c1784caf77 Datum/Tijd: 25-03-2024 12:37:32 Valutadatum: 25-03-2024"
    )]
    [Arguments(
        "Name: SomeCompany B.V. Description: Payment IBAN: EE406224654075974290 Reference: df1c6177f0ed4e81a2dea3c1784caf77 Date/time: 25-03-2024 12:37:32 Value date: 25/03/2024"
    )]
    public async Task ParsesTransfer(string note)
    {
        var result = _parser.ParseNote(note);

        await Assert.That(result.Name).IsEqualTo("SomeCompany B.V.");
        await Assert.That(result.Description).IsEqualTo("Payment");
        await Assert.That(result.Iban).IsEqualTo("EE406224654075974290");
        await Assert.That(result.Reference).IsEqualTo("df1c6177f0ed4e81a2dea3c1784caf77");

        await Assert.That(result.DateTime).IsEqualTo(new DateTime(2024, 3, 25, 12, 37, 32));
        await Assert.That(result.ValueDate).IsEqualTo(new DateOnly(2024, 3, 25));
    }
}
