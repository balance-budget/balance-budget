using System.Reflection;
using Balance.Services.Contracts;
using Microsoft.Extensions.Hosting;

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
