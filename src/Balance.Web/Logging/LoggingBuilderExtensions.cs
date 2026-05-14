using Balance.Configuration.Helpers;

namespace Balance.Web.Logging;

internal static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddConsole(
        this ILoggingBuilder builder,
        IHostEnvironment environment
    ) =>
        environment.IsContainer()
            ? builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            })
            : builder.AddConsole();
}
