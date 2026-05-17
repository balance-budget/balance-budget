using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Balance.Tests.Api.Helpers;

internal static class JsonPatchHelpers
{
    public const string MediaType = "application/json-patch+json";

    public static Task<HttpResponseMessage> PatchAsJsonPatchAsync(
        this HttpClient client,
        Uri uri,
        IEnumerable<object> operations
    )
    {
        var json = JsonSerializer.Serialize(operations);
        return PatchAsJsonPatchRawAsync(client, uri, json);
    }

    public static async Task<HttpResponseMessage> PatchAsJsonPatchRawAsync(
        this HttpClient client,
        Uri uri,
        string rawJson
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        using var content = new StringContent(rawJson, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
        return await client.PatchAsync(uri, content);
    }

    public static object Replace(string path, object? value) =>
        new
        {
            op = "replace",
            path,
            value,
        };

    public static object Remove(string path) => new { op = "remove", path };

    public static object Add(string path, object? value) =>
        new
        {
            op = "add",
            path,
            value,
        };
}
