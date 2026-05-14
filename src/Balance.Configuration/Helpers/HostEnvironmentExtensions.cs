using Microsoft.Extensions.Hosting;

namespace Balance.Configuration.Helpers;

public static class HostEnvironmentExtensions
{
    public static bool IsContainer(this IHostEnvironment hostEnvironment) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    public static bool IsContainerFastMode(this IHostEnvironment hostEnvironment) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER_FAST_MODE") == "true";
}
