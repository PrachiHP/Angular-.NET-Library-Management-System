namespace BackendAPI.Exceptions;

/// <summary>
/// Base of the application's exception hierarchy. Services throw these instead
/// of returning error codes, so the HTTP status is decided in exactly one place
/// (GlobalExceptionMiddleware) rather than in every controller action.
/// </summary>
public abstract class AppException : Exception
{
    /// <summary>The HTTP status this exception maps to.</summary>
    public abstract int StatusCode { get; }

    protected AppException(string message) : base(message)
    {
    }

    // Preserving the inner exception keeps the original stack trace for logs.
    protected AppException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The requested entity does not exist. Maps to 404.</summary>
public class NotFoundException : AppException
{
    public override int StatusCode => StatusCodes.Status404NotFound;

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entity, int id)
        : base($"{entity} with id {id} was not found.") { }
}

/// <summary>
/// The request was well formed but breaks a business rule — issuing a book the
/// member already holds, exceeding the re-issue limit. Maps to 422.
/// </summary>
public class BusinessException : AppException
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;

    public BusinessException(string message) : base(message) { }

    public BusinessException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>Input failed validation. Maps to 400.</summary>
public class ValidationException : AppException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;

    /// <summary>Field name to error messages, mirroring ModelState's shape.</summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

/// <summary>Caller is not permitted to do this. Maps to 401.</summary>
public class UnauthorizedException : AppException
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;

    public UnauthorizedException(string message) : base(message) { }
}
