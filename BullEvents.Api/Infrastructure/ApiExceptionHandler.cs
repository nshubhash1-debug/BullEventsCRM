using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BullEvents.Api.Infrastructure;

/// <summary>Thrown by controllers for expected, client-correctable failures.</summary>
public class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;

    public static ApiException NotFound(string what) => new(404, $"{what} was not found.");
    public static ApiException BadRequest(string message) => new(400, message);
    public static ApiException Conflict(string message) => new(409, message);
    public static ApiException Forbidden(string message) => new(403, message);
}

/// <summary>
/// One exit path for every unhandled failure. Clients always get the same
/// shape — `{ message, traceId }` — and the stack only ever reaches the log.
/// </summary>
public class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            ApiException api => (api.Status, api.Message),
            ArgumentException or FormatException => (400, exception.Message),
            UnauthorizedAccessException => (403, "You do not have access to this resource."),
            _ => (500, "Something went wrong handling this request."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected ({Status}) on {Method} {Path}: {Message}",
                status, context.Request.Method, context.Request.Path, message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status >= 500 ? "Server error" : "Request could not be completed",
            Detail = message,
            Instance = context.Request.Path,
        };

        problem.Extensions["message"] = message;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (environment.IsDevelopment() && status >= 500)
        {
            problem.Extensions["exception"] = exception.ToString();
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
