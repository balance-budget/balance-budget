using Balance.Data.Exceptions;
using Microsoft.Extensions.Logging;

namespace Balance.Web.Logging;

internal static partial class LoggerExtensions
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Domain exception thrown ({Kind}): {DomainMessage}"
    )]
    public static partial void DomainExceptionThrown(
        this ILogger logger,
        DomainExceptionKind kind,
        string domainMessage
    );
}
