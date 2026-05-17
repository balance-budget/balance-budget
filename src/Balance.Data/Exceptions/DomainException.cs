namespace Balance.Data.Exceptions;

public class DomainException : Exception
{
    public DomainExceptionKind Kind { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public DomainException()
    {
        Kind = DomainExceptionKind.Invariant;
    }

    public DomainException(string message)
        : base(message)
    {
        Kind = DomainExceptionKind.Invariant;
    }

    public DomainException(DomainExceptionKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public DomainException(
        DomainExceptionKind kind,
        string message,
        IReadOnlyDictionary<string, string[]> errors
    )
        : base(message)
    {
        Kind = kind;
        Errors = errors;
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = DomainExceptionKind.Invariant;
    }

    public DomainException(DomainExceptionKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }
}
