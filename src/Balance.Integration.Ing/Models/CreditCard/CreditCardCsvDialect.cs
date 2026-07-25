using System.Globalization;

namespace Balance.Integration.Ing.Models.CreditCard;

// The language ING exported a credit-card CSV in. Not a separate Statement layout: the file
// structure is identical and only its rendering differs (ADR 0038). It has to be detected per file
// because the export switches *number* culture with the language — a rate of "0.0000489" read under
// nl-NL becomes 489 — and because the note vocabulary and date format follow suit.
internal enum CreditCardCsvDialect
{
    Dutch,
    English,
}

internal static class CreditCardCsvDialects
{
    public static CultureInfo Culture(this CreditCardCsvDialect dialect) =>
        dialect is CreditCardCsvDialect.Dutch
            ? CultureInfo.GetCultureInfo("nl-NL")
            : CultureInfo.GetCultureInfo("en-US");

    // The note's transaction date. The Date column is ISO in both dialects; only this one differs.
    public static string NoteDateFormat(this CreditCardCsvDialect dialect) =>
        dialect is CreditCardCsvDialect.Dutch ? "dd-MM-yyyy" : "dd/MM/yyyy";
}
