using Balance.Configuration.Contracts;

namespace Balance.Configuration.Options;

public sealed class JobsOptions : IOptionsSection
{
    public static string Section => "Jobs";

    public required bool RunJobs { get; init; }
}
