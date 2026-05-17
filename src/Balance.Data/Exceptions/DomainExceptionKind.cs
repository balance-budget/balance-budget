namespace Balance.Data.Exceptions;

public enum DomainExceptionKind
{
    /// <summary>A domain invariant was violated. Maps to HTTP 422.</summary>
    Invariant,

    /// <summary>The request shape was invalid. Maps to HTTP 400.</summary>
    Validation,

    /// <summary>An entity was not found. Maps to HTTP 404.</summary>
    NotFound,

    /// <summary>An entity conflicts with an existing one (e.g. unique violation). Maps to HTTP 409.</summary>
    Conflict,
}
