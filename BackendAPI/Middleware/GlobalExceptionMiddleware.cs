using System.Text.Json;
using BackendAPI.Dtos;
using BackendAPI.Exceptions;

namespace BackendAPI.Middleware;

/// <summary>
/// Catches everything the pipeline throws and converts it to one consistent
/// JSON error shape. Registered first so it wraps every later middleware.
///
/// Without this, every controller action would need its own try/catch, and the
/// mapping from exception to status code would be duplicated everywhere.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass control to the next middleware. Everything downstream —
            // routing, the controller, the service, EF Core — runs inside
            // this call, so any throw lands in the catch blocks below.
            await _next(context);
        }
        catch (ValidationException ex)
        {
            // Most specific first: ValidationException carries a field->errors
            // dictionary the others do not.
            _logger.LogWarning(ex, "Validation failed on {Path}", context.Request.Path);
            await WriteAsync(context, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (AppException ex)
        {
            // Any of our deliberate business failures. Expected, so logged at
            // Warning rather than Error — these are not bugs.
            _logger.LogWarning(ex, "{ExceptionType} on {Path}", ex.GetType().Name, context.Request.Path);
            await WriteAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            // Anything unplanned. This one IS a bug, so log at Error with the
            // full stack trace, and never leak internals to the caller.
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);

            var message = _environment.IsDevelopment()
                ? ex.Message
                : "An unexpected error occurred.";

            await WriteAsync(context, StatusCodes.Status500InternalServerError, message);
        }
    }

    private static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string message,
        IDictionary<string, string[]>? errors = null)
    {
        // If the response has already started streaming, headers are gone and
        // there is nothing safe to do but let it fail.
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new ErrorResponse(statusCode, message, errors is { Count: > 0 } ? errors : null);

        await context.Response.WriteAsync(JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}

/// <summary>Extension method so Program.cs reads app.UseGlobalExceptionHandling().</summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
