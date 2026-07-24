namespace Balance.Integration.Stater.Models;

// A reconstructed visual line: the space-joined text (for section/anchor detection) plus the
// words with their horizontal extent (for column-band bucketing).
internal sealed record StaterTextLine(string Text, IReadOnlyList<StaterWord> Words);

internal sealed record StaterWord(string Text, double Left, double Right)
{
    public double Center => (Left + Right) / 2;
}
