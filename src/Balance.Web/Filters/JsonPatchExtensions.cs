using Balance.Data.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace Balance.Web.Filters;

internal static class JsonPatchExtensions
{
    /// <summary>
    /// Applies <paramref name="patch"/> to <paramref name="snapshot"/>, collecting apply-time
    /// errors as a <see cref="DomainException"/>; on success, runs <paramref name="validator"/>
    /// against the patched snapshot and surfaces FluentValidation failures the same way.
    /// </summary>
    public static async Task<T> ApplyAndValidateAsync<T>(
        this JsonPatchDocument<T> patch,
        T snapshot,
        IValidator<T>? validator,
        CancellationToken cancellationToken
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(snapshot);

        Dictionary<string, string[]>? applyErrors = null;
        patch.ApplyTo(
            snapshot,
            error =>
            {
                applyErrors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                var path = error.Operation?.path ?? string.Empty;
                applyErrors[path] = applyErrors.TryGetValue(path, out var existing)
                    ? [.. existing, error.ErrorMessage]
                    : [error.ErrorMessage];
            }
        );

        if (applyErrors is not null)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                "JSON Patch could not be applied.",
                applyErrors
            );
        }

        if (validator is not null)
        {
            var result = await validator.ValidateAsync(snapshot, cancellationToken);
            if (!result.IsValid)
            {
                var errors = result
                    .Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                throw new DomainException(
                    DomainExceptionKind.Validation,
                    "Validation failed.",
                    errors
                );
            }
        }

        return snapshot;
    }
}
