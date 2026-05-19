using System.Net;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class HealthCheckEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task Liveness_probe_returns_200_when_app_is_up()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/healthz/live", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Readiness_probe_returns_200_when_database_is_reachable()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/healthz/ready", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Legacy_root_healthz_route_is_removed()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/healthz", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
