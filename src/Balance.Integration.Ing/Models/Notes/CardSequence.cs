using System.Globalization;

namespace Balance.Integration.Ing.Models.Notes;

internal sealed class CardSequence
{
    public string SequenceNumber { get; }
    public DateTime DateTime { get; }

    private CardSequence(string sequenceNumber, DateTime dateTime)
    {
        SequenceNumber = sequenceNumber;
        DateTime = dateTime;
    }

    /// <summary>
    /// Parses a card-sequence note value of the form <c>"008 12-01-2019 15:26"</c>.
    /// Returns <c>null</c> when the value is missing the date/time tail or the date
    /// fails to parse — keeps a single malformed ING row from sinking a whole import.
    /// </summary>
    internal static CardSequence? TryParse(string value)
    {
        var parts = value.Split(' ', 2);
        if (parts.Length < 2)
            return null;

        if (!DateTime.TryParse(parts[1], CultureInfo.GetCultureInfo("nl-NL"), out var date))
            return null;

        return new CardSequence(parts[0], date);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{SequenceNumber} {DateTime}");
}
