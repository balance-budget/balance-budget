using Balance.Configuration.Contracts;

namespace Balance.Configuration.Options;

public sealed class AuthOptions : IOptionsSection
{
    public static string Section => "Auth";

    /// <summary>
    /// Deploy-time secret guarding the first-run setup wizard. The wizard refuses requests
    /// unless this token is supplied (header or query), <em>and</em> the user table is empty
    /// (ADR 0018). May be null in non-production environments; the wizard then accepts any
    /// (or absent) token, since the empty-table guard is still in effect.
    /// </summary>
    public string? SetupToken { get; init; }
}
