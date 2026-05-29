using System.Net.Http.Json;

namespace Balance.Tests.Api.Helpers;

internal sealed record PagedDto<T>(IReadOnlyList<T> Items, int TotalCount);

internal static class PagedHttpContentExtensions
{
    public static async Task<IReadOnlyList<T>> ReadPagedItemsAsync<T>(this HttpContent content)
    {
        var paged = await content.ReadFromJsonAsync<PagedDto<T>>();
        return paged?.Items ?? throw new InvalidOperationException("Paged response was null.");
    }
}
