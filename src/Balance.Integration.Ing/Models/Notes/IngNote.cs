using System.Globalization;
using System.Text;

namespace Balance.Integration.Ing.Models.Notes;

internal sealed class IngNote
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Iban { get; set; }
    public CardSequence? CardSequence { get; set; }
    public string? Transaction { get; set; }
    public string? Term { get; set; }
    public CurrencyAmount? ForeignCurrencyAmount { get; set; }
    public decimal ForeignCurrencyRate { get; set; }
    public CurrencyAmount? ForeignCurrencyMarkUp { get; set; }
    public CurrencyAmount? ForeignCurrencyFee { get; set; }
    public DateOnly? ValueDate { get; set; }
    public DateTime? DateTime { get; set; }
    public string? Reference { get; set; }
    public string? MandateId { get; set; }
    public SepaDirectDebitCreditor? Creditor { get; set; }
    public string? OtherParty { get; set; }
    public string? Other { get; set; }
    public required string Original { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        Append("Name", Name);
        Append("Description", Description);
        Append("IBAN", Iban);
        Append("Card Sequence", CardSequence);
        Append("Transaction", Transaction);
        Append("Term", Term);
        Append("Foreign Currency Amount", ForeignCurrencyAmount);
        Append("Foreign Currency Rate", ForeignCurrencyRate);
        Append("Foreign Currency Mark Up", ForeignCurrencyMarkUp);
        Append("Foreign Currency Fee", ForeignCurrencyFee);
        Append("Value Date", ValueDate);
        Append("Date / Time", DateTime);
        Append("Reference", Reference);
        Append("Mandate ID", MandateId);
        Append("Creditor", Creditor);
        Append("Other Party", OtherParty);
        Append("Other", Other);

        return sb.ToString();

        void Append(string label, object? value)
        {
            if (value is null or "" or 0m)
                return;
            sb.AppendLine(CultureInfo.InvariantCulture, $"{label}: {value}");
        }
    }
}
