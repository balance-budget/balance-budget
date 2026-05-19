using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Balance.Configuration.Helpers;

public static class HostEnvironmentExtensions
{
    public static bool IsContainer(this IHostEnvironment _) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    public static bool IsContainerFastMode(this IHostEnvironment _) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER_FAST_MODE") == "true";

    public static bool IsIntegrationTest(this IHostEnvironment hostEnvironment) =>
        hostEnvironment.IsEnvironment("IntegrationTest");

    public static bool IsOpenApiGenerator(this IHostEnvironment _) =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
