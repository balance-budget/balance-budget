using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Balance.Services.Contracts;

/// <summary>
/// Closed set of dashboard trend-range tokens. The wire form is the short code
/// (<c>1M</c>, <c>3M</c>, <c>6M</c>, <c>1Y</c>) for both query-string binding (via
/// <see cref="IParsable{TrendRange}"/>) and JSON output (via the record-struct's
/// <see cref="Value"/>). Only the four <c>public static</c> instances are reachable —
/// the constructor is private and <see cref="TryParse(string?, IFormatProvider?, out TrendRange)"/>
/// is the only entry point for callers.
/// </summary>
public readonly record struct TrendRange : IParsable<TrendRange>
{
    public static TrendRange OneMonth { get; } = new("1M");
    public static TrendRange ThreeMonths { get; } = new("3M");
    public static TrendRange SixMonths { get; } = new("6M");
    public static TrendRange OneYear { get; } = new("1Y");

    public string Value { get; }

    private TrendRange(string value) => Value = value;

    public static TrendRange Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result)
            ? result
            : throw new FormatException(
                $"'{s}' is not a valid TrendRange. Expected one of: 1M, 3M, 6M, 1Y."
            );

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out TrendRange result
    )
    {
        switch (s?.ToUpperInvariant())
        {
            case "1M":
                result = OneMonth;
                return true;
            case "3M":
                result = ThreeMonths;
                return true;
            case "6M":
                result = SixMonths;
                return true;
            case "1Y":
                result = OneYear;
                return true;
            default:
                result = default;
                return false;
        }
    }

    public override string ToString() => Value;

    internal int Months =>
        Value switch
        {
            "1M" => 1,
            "3M" => 3,
            "6M" => 6,
            "1Y" => 12,
            _ => throw new UnreachableException($"Unknown TrendRange value '{Value}'."),
        };
}
