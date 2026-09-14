namespace BackendAPI.Dtos;

/// <summary>
/// One consistent error shape for every failure, so the Angular client has a
/// single contract to handle rather than guessing per endpoint.
/// </summary>
public record ErrorResponse(
    int Status,
    string Message,
    IDictionary<string, string[]>? Errors = null);
