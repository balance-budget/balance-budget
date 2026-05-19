using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Balance.Configuration.Helpers;

public static class HostEnvironmentExtensions
{
    public static bool IsContainer(this IHostEnvironment _) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    public static bool IsContainerFastMode(this IHostEnvironment _) =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER_FAST_MODE") == "true";

    /// <summary>
    /// Checks if the application is running for integration testing
    /// </summary>
    /// <param name="hostEnvironment"></param>
    /// <returns></returns>
    public static bool IsIntegrationTest(this IHostEnvironment hostEnvironment) =>
        hostEnvironment.IsEnvironment("IntegrationTest");

    /// <summary>
    /// Checks if the application is running at design time
    /// For example, when generating an OpenAPI document viaMicrosoft.Extensions.ApiDescription.Server
    /// </summary>
    /// <param name="_"></param>
    /// <returns></returns>
    public static bool IsDesignTime(this IHostEnvironment _) =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
