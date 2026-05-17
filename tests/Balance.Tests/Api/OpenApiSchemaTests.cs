using System.Net;
using System.Text.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class OpenApiSchemaTests : EndpointsTestsBase
{
    [Test]
    public async Task Typed_guid_id_schema_is_flattened_to_string_uuid()
    {
        var document = await GetOpenApiDocumentAsync();
        var schema = GetComponentSchema(document, "AccountId");

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(schema.GetProperty("format").GetString()).IsEqualTo("uuid");
        await Assert.That(schema.TryGetProperty("properties", out _)).IsFalse();
    }

    [Test]
    public async Task CurrencyCode_schema_is_flattened_to_plain_string()
    {
        var document = await GetOpenApiDocumentAsync();
        var schema = GetComponentSchema(document, "CurrencyCode");

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(schema.TryGetProperty("format", out _)).IsFalse();
        await Assert.That(schema.TryGetProperty("properties", out _)).IsFalse();
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonElement GetComponentSchema(JsonDocument document, string name)
    {
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        return schemas.GetProperty(name);
    }
}
