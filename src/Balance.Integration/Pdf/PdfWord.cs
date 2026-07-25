namespace Balance.Integration.Pdf;

// A single word with its position on the page. PDF coordinates put the origin at the bottom-left,
// so larger Y is higher up and Top is above Bottom.
public sealed record PdfWord(string Text, double Left, double Right, double Top, double Bottom)
{
    public double CenterX => (Left + Right) / 2;

    public double Height => Top - Bottom;
}
