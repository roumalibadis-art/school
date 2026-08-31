namespace USTHBStudy.Application.Common;

/// <summary>
/// Base type for expected application faults. The API exception middleware (PRD §45) maps each
/// subtype to an HTTP status and the standard error body; internal details are logged, never returned.
/// </summary>
public class AppException : Exception
{
    public AppException(string message, IEnumerable<string>? errors = null)
        : base(message)
    {
        Errors = errors?.ToArray() ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>400 — the request is malformed or violates a business rule.</summary>
public sealed class BadRequestException : AppException
{
    public BadRequestException(string message, IEnumerable<string>? errors = null) : base(message, errors)
    {
    }
}

/// <summary>400 — one or more input validation errors (PRD §45).</summary>
public sealed class ValidationAppException : AppException
{
    public ValidationAppException(IEnumerable<string> errors)
        : base("One or more validation errors occurred.", errors)
    {
    }
}

/// <summary>401 — authentication is missing or invalid.</summary>
public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Authentication is required.") : base(message)
    {
    }
}

/// <summary>403 — authenticated but not allowed (suspended, wrong role, no Premium — PRD §23/§61).</summary>
public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message = "You do not have access to this resource.") : base(message)
    {
    }
}

/// <summary>404 — the target resource does not exist.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entity, object key) : base($"{entity} '{key}' was not found.")
    {
    }
}

/// <summary>409 — the request conflicts with current state (e.g. duplicate email).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message)
    {
    }
}
