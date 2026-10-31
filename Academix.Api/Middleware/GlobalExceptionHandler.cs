using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academix.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            ApiFault fault => fault.StatusCode,
            DbUpdateConcurrencyException => 409,
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" } } => 409,
            _ => 500
        };
        if (status >= 500) logger.LogError(exception, "Unexpected API error at {Path}", httpContext.Request.Path);
        await Results.Problem(title: status == 500 ? "Unexpected server error" : "Request rejected",
            detail: status == 500 ? "An unexpected error occurred." : exception.Message,
            statusCode: status).ExecuteAsync(httpContext);
        return true;
    }
}
