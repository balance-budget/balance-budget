using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Balance.Data.Helpers;

internal static class DateConverters
{
    internal static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    );

    internal static readonly ValueConverter<DateTime?, DateTime?> UtcNullableConverter = new(
        v =>
            v == null ? v
            : v.Value.Kind == DateTimeKind.Utc ? v
            : v.Value.ToUniversalTime(),
        v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
    );
}
