using Microsoft.AspNetCore.Hosting;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Helpers;

internal class WebApplicationFactory : TestWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("IntegrationTest");
    }
}
