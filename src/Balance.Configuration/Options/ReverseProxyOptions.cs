using Balance.Configuration.Contracts;

namespace Balance.Configuration.Options;

public sealed class ReverseProxyOptions : IOptionsSection
{
    public static string Section => "ReverseProxy";

    public required IReadOnlyList<string> KnownNetworks { get; init; } = [];
}
