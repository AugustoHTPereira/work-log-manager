using System.Text.Json;
using FluentValidation;
using WorkLogManager.Application.Common;

namespace WorkLogManager.Api.Middleware;

/// <summary>
/// Single exception handling middleware for the Api: translates Application-level
/// exceptions into the corresponding HTTP status codes.
/// </summary>
public class ExceptionHandlingMiddleware
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
        catch (NotFoundException exception)
        {
            _logger.LogInformation(exception, "Not found.");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = exception.Message });
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(exception, "Domain validation error.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = exception.Message });
        }
        catch (ValidationException exception)
        {
            _logger.LogInformation(exception, "Input validation error.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var message = string.Join(" ", exception.Errors.Select(e => e.ErrorMessage));
            await context.Response.WriteAsJsonAsync(new { message });
        }
        catch (JsonException exception)
        {
            _logger.LogInformation(exception, "Invalid JSON payload.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid request payload." });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request aborted by the client.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}.", context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                message = exception.Message,
                detail = exception.InnerException?.Message,
                exceptionType = exception.GetType().FullName,
                stackTrace = exception.StackTrace,
            });
        }
    }
}
