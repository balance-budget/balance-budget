using Balance.Configuration.Contracts;
using Microsoft.Extensions.Configuration;

namespace Balance.Configuration.Helpers;

public static class ConfigurationExtensions
{
    public static T GetSection<T>(this IConfiguration configuration)
        where T : class, IOptionsSection
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetSection(T.Section).Get<T>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{T.Section}' is missing or invalid."
            );
    }

    public static T? GetSectionOrDefault<T>(this IConfiguration configuration)
        where T : class, IOptionsSection
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetSection(T.Section).Get<T>();
    }
}
