using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Common;

public sealed class AppExceptionHandler(
    IHostEnvironment environment,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationAppException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenAppException => (StatusCodes.Status403Forbidden, "Forbidden"),
            NotFoundAppException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictAppException => (StatusCodes.Status409Conflict, "Conflict"),
            AppException => (StatusCodes.Status400BadRequest, "Request failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception. Method: {Method}; Path: {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError && !environment.IsDevelopment()
                ? null
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationAppException { Errors.Count: > 0 } validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
