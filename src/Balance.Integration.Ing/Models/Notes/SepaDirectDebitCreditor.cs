namespace Balance.Integration.Ing.Models.Notes;

internal sealed class SepaDirectDebitCreditor
{
    public string Id { get; }
    public string Description { get; }

    private SepaDirectDebitCreditor(string id, string description)
    {
        Id = id;
        Description = description;
    }

    internal static SepaDirectDebitCreditor Parse(string value)
    {
        var parts = value.Split(' ', 2);
        return new SepaDirectDebitCreditor(parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    public override string ToString() => $"{Id} {Description}";
}
