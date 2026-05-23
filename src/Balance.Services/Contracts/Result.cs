using System.Diagnostics.CodeAnalysis;

namespace Balance.Services.Contracts;

/// <summary>
/// Result of a service operation that may fail with a <see cref="ServiceError"/>.
/// Use the non-generic <see cref="Result"/> for side-effect-only operations.
/// Construct via the public constructors or via the implicit conversions from
/// <typeparamref name="T"/> and <see cref="ServiceError"/>; the implicit conversion
/// from <typeparamref name="T"/> doesn't fire when <typeparamref name="T"/> is an
/// interface type (a C# user-defined conversion limitation) — use the constructor there.
/// </summary>
public readonly record struct Result<T>
{
    public T? Value { get; }
    public ServiceError? Error { get; }

    public Result(T value)
    {
        Value = value;
        Error = null;
    }

    public Result(ServiceError error)
    {
        Value = default;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(ServiceError error) => new(error);
}

/// <summary>
/// Result of a side-effect-only service operation (e.g. <c>DeleteAsync</c>) that may fail with
/// a <see cref="ServiceError"/>. Equivalent to <c>Result&lt;Unit&gt;</c> without the Unit type.
/// </summary>
public readonly record struct Result
{
    public ServiceError? Error { get; }

    public Result(ServiceError error)
    {
        Error = error;
    }

    public static readonly Result Success;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static implicit operator Result(ServiceError error) => new(error);
}
