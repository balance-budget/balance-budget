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

    internal static CardSequence Parse(string value)
    {
        var parts = value.Split(' ', 2);
        var sequenceNumber = parts[0];
        var date = DateTime.Parse(parts[1], CultureInfo.GetCultureInfo("nl-NL"));

        return new CardSequence(sequenceNumber, date);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{SequenceNumber} {DateTime}");
}
