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

    [Test]
    public async Task Patch_account_endpoint_references_per_target_schema()
    {
        var document = await GetOpenApiDocumentAsync();
        var schemaRef =
            document
                .RootElement.GetProperty("paths")
                .GetProperty("/api/accounts/{id}")
                .GetProperty("patch")
                .GetProperty("requestBody")
                .GetProperty("content")
                .GetProperty("application/json-patch+json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString()
            ?? string.Empty;

        await Assert
            .That(schemaRef)
            .IsEqualTo("#/components/schemas/JsonPatchDocumentOfUpdateAccountInput");
    }

    [Test]
    public async Task Patch_account_schema_constrains_path_to_target_properties()
    {
        var document = await GetOpenApiDocumentAsync();
        var schema = GetComponentSchema(document, "JsonPatchDocumentOfUpdateAccountInput");

        var pathEnum = schema
            .GetProperty("items")
            .GetProperty("oneOf")[0]
            .GetProperty("properties")
            .GetProperty("path")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();

        await Assert
            .That(pathEnum)
            .IsEquivalentTo([
                "/name",
                "/code",
                "/accountType",
                "/currencyCode",
                "/isPostable",
                "/isLiquid",
                "/horizon",
                "/parentAccountId",
                "/iconName",
            ]);
    }

    [Test]
    public async Task Patch_account_target_shape_is_present_in_components()
    {
        var document = await GetOpenApiDocumentAsync();
        var schema = GetComponentSchema(document, "UpdateAccountInput");

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("object");
        var propertyNames = schema
            .GetProperty("properties")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToArray();
        await Assert
            .That(propertyNames)
            .IsEquivalentTo([
                "name",
                "code",
                "accountType",
                "currencyCode",
                "isPostable",
                "isLiquid",
                "horizon",
                "parentAccountId",
                "iconName",
            ]);
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/api/openapi/v1.json", UriKind.Relative)
        );
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
