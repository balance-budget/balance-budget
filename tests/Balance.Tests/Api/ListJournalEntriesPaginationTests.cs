using System.Net;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class ListJournalEntriesPaginationTests : EndpointsTestsBase
{
    [Test]
    public async Task Negative_skip_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/journal-entries?skip=-1", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Take_above_max_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/journal-entries?take=201", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Take_zero_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/journal-entries?take=0", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Defaults_return_200()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/journal-entries", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
