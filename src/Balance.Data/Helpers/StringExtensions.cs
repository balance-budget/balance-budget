namespace Balance.Data.Helpers;

internal static class StringExtensions
{
    public static string? TrimToNull(this string? value)
    {
        if (value is null)
            return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
