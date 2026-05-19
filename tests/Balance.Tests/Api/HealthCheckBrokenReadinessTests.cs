using System.Net;
using Balance.Tests.Api.Helpers;
using TUnit.AspNetCore;

namespace Balance.Tests.Api;

internal sealed class HealthCheckBrokenReadinessTests
    : WebApplicationTest<BrokenReadinessWebApplicationFactory, Program>
{
    [Test]
    public async Task Liveness_probe_stays_200_even_when_readiness_checks_fail()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/healthz/live", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Readiness_probe_returns_503_when_a_readiness_check_fails()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/healthz/ready", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }
}
