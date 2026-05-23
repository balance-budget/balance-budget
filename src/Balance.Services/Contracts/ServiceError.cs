namespace Balance.Services.Contracts;

/// <summary>
/// Closed hierarchy of service-layer errors returned via <see cref="Result{T}"/> / <see cref="Result"/>.
/// Each variant carries a stable <see cref="Code"/> string used as the <c>code</c> extension on
/// the wire <c>ProblemDetails</c>; see <see cref="ErrorCodes"/> for the common codes.
/// </summary>
public abstract record ServiceError(string Code);

public sealed record NotFoundError(string Entity, string Id) : ServiceError(ErrorCodes.NotFound);

public sealed record ConflictError(string Code, string Message) : ServiceError(Code);

public sealed record InvariantError(string Code, string Message) : ServiceError(Code);

public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : ServiceError(ErrorCodes.RequestInvalid);
