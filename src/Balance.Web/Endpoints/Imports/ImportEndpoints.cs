using Balance.Services.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Imports;

internal static class ImportEndpoints
{
    public const string PathPrefix = "/imports";

    public static void MapImports(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Imports");
        // Drop-and-detect (ADR 0034): N files, no chosen account. Detection resolves each file's
        // target and imports the unambiguous ones; the rest come back for manual resolution via
        // the per-account route. Antiforgery is opted out to match the other multipart POSTs.
        group
            .MapPost("", DetectAndImportAsync)
            .DisableAntiforgery()
            .WithName("DetectAndImportStatements");
    }

    private static async Task<
        Results<Ok<IReadOnlyList<DetectedImportOutcome>>, ValidationProblem>
    > DetectAndImportAsync(
        IFormFileCollection files,
        [FromServices] IBankStatementDetectionService detectionService,
        CancellationToken cancellationToken
    )
    {
        if (files.Count == 0)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["files"] = ["Upload at least one file."] }
            );
        }

        // Buffer each upload into a seekable stream so the detection probe and the eventual import
        // can both read it from the start.
        var importFiles = new List<ImportFile>(files.Count);
        var streams = new List<MemoryStream>(files.Count);
        try
        {
            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                var buffer = new MemoryStream();
                streams.Add(buffer);
                await file.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
                importFiles.Add(new ImportFile(file.FileName, buffer));
            }

            if (importFiles.Count == 0)
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { ["files"] = ["All files were empty."] }
                );
            }

            var outcomes = await detectionService.DetectAndImportAsync(
                importFiles,
                cancellationToken
            );
            return TypedResults.Ok(outcomes);
        }
        finally
        {
            foreach (var buffer in streams)
                await buffer.DisposeAsync();
        }
    }
}
