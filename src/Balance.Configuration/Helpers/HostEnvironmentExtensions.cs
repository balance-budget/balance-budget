using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Balance.Configuration.Helpers;

public static class HostEnvironmentExtensions
{
    public static bool IsContainer(this IHostEnvironment hostEnvironment) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    public static bool IsContainerFastMode(this IHostEnvironment hostEnvironment) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER_FAST_MODE") == "true";

    public static bool IsTesting(this IHostEnvironment hostEnvironment) =>
        hostEnvironment.IsEnvironment("Testing");

    public static bool IsRunningFrom<T>(this IHostEnvironment _) =>
        Assembly.GetEntryAssembly() == typeof(T).Assembly;
}
