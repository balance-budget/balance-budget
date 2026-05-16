using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class CounterpartyEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListCounterparties_returns_ok()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/counterparties", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var counterparties = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<CounterpartyDto>
        >();
        await Assert.That(counterparties).IsNotNull();
    }

    [Test]
    public async Task CreateCounterparty_round_trips()
    {
        using var client = Factory.CreateClient();

        var request = new CreateCounterpartyRequestDto("Albert Heijn");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Name).IsEqualTo("Albert Heijn");

        using var getResponse = await client.GetAsync(
            new Uri($"/counterparties/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<CounterpartyDto>();
        await Assert.That(fetched!.Id).IsEqualTo(created.Id);
        await Assert.That(fetched.Name).IsEqualTo("Albert Heijn");

        using var listResponse = await client.GetAsync(
            new Uri("/counterparties", UriKind.Relative)
        );
        var list = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<CounterpartyDto>>();
        await Assert.That(list!.Select(c => c.Name)).Contains("Albert Heijn");
    }

    [Test]
    public async Task CreateCounterparty_duplicate_name_case_insensitive_returns_409()
    {
        using var client = Factory.CreateClient();

        var first = new CreateCounterpartyRequestDto("Jumbo Supermarket");
        using var firstResponse = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            first
        );
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var duplicate = new CreateCounterpartyRequestDto("jumbo supermarket");
        using var duplicateResponse = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            duplicate
        );

        await Assert.That(duplicateResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task GetCounterparty_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        var unknownId = Guid.NewGuid();
        using var response = await client.GetAsync(
            new Uri($"/counterparties/{unknownId}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateCounterparty_empty_name_returns_400()
    {
        using var client = Factory.CreateClient();

        var request = new CreateCounterpartyRequestDto("");
        using var response = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateCounterparty_renames()
    {
        using var client = Factory.CreateClient();

        var request = new CreateCounterpartyRequestDto("Counterparty To Rename");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyDto>();

        var update = new UpdateCounterpartyRequestDto("Renamed Counterparty");
        using var patchResponse = await client.PatchAsJsonAsync(
            new Uri($"/counterparties/{created!.Id}", UriKind.Relative),
            update
        );

        await Assert.That(patchResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await patchResponse.Content.ReadFromJsonAsync<CounterpartyDto>();
        await Assert.That(updated!.Name).IsEqualTo("Renamed Counterparty");
    }

    [Test]
    public async Task UpdateCounterparty_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        var update = new UpdateCounterpartyRequestDto("Whatever");
        using var response = await client.PatchAsJsonAsync(
            new Uri($"/counterparties/{Guid.NewGuid()}", UriKind.Relative),
            update
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteCounterparty_removes_the_row()
    {
        using var client = Factory.CreateClient();

        var request = new CreateCounterpartyRequestDto("Temporary Counterparty");
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/counterparties", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<CounterpartyDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/counterparties/{created!.Id}", UriKind.Relative)
        );

        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/counterparties/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by System.Text.Json deserialization."
)]
internal sealed record CounterpartyDto(Guid Id, string Name);

internal sealed record CreateCounterpartyRequestDto(string Name);

internal sealed record UpdateCounterpartyRequestDto(string? Name);
