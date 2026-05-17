using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Balance.Tests.Api.Helpers;

internal sealed class BrokenReadinessWebApplicationFactory : WebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services
                .AddHealthChecks()
                .AddCheck(
                    "simulated-readiness-failure",
                    () => HealthCheckResult.Unhealthy("simulated downstream failure"),
                    tags: ["readiness"]
                );
        });
    }
}
