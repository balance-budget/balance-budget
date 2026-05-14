using System.Reflection;
using Microsoft.Extensions.Hosting;
using Balance.Services.Contracts;

namespace Balance.Services;

internal sealed class ApplicationVersionService : IApplicationVersionService
{
    private const string DefaultVersion = "0.0.0";
    public string Version { get; }

    public ApplicationVersionService(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        Version =
            Assembly
                .GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? DefaultVersion;
    }
}
