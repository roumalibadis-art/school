namespace USTHBStudy.API.Middleware;

using USTHBStudy.Application.Common;

/// <summary>
/// Centralized exception handling (PRD §45). Maps <see cref="AppException"/> subtypes to HTTP
/// status codes and the standard error body; logs everything server-side; never returns stack traces.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — nothing to return.
            _logger.LogDebug("Request {Method} {Path} was cancelled by the client.", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, message, errors) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled {Exception} on {Method} {Path}: {Message}",
                exception.GetType().Name, context.Request.Method, context.Request.Path, exception.Message);
        }

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; cannot write error body.");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(message, errors));
    }

    private static (int Status, string Message, IReadOnlyList<string> Errors) Map(Exception exception) => exception switch
    {
        ValidationAppException ex => (StatusCodes.Status400BadRequest, ex.Message, ex.Errors),
        BadRequestException ex => (StatusCodes.Status400BadRequest, ex.Message, ex.Errors),
        UnauthorizedAppException ex => (StatusCodes.Status401Unauthorized, ex.Message, ex.Errors),
        ForbiddenAppException ex => (StatusCodes.Status403Forbidden, ex.Message, ex.Errors),
        NotFoundException ex => (StatusCodes.Status404NotFound, ex.Message, ex.Errors),
        ConflictException ex => (StatusCodes.Status409Conflict, ex.Message, ex.Errors),
        AppException ex => (StatusCodes.Status400BadRequest, ex.Message, ex.Errors),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", Array.Empty<string>()),
    };
}
