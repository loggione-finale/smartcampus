using System.Net.Sockets;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace SmartCampus.Users.Api.Infrastructure;

public sealed class DatabaseUnavailableExceptionHandler(ILogger<DatabaseUnavailableExceptionHandler> logger) : IExceptionHandler
{
    private static readonly string[] UnavailableSqlStates = ["57P01", "57P02", "57P03", "53300"];

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsDatabaseUnavailable(exception))
        {
            return false;
        }

        logger.LogWarning(exception, "Database not reachable while handling {Path}.", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = "5";
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Database unavailable",
                Detail = "The database is not reachable. Try again shortly."
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
        return true;
    }

    public static bool IsDatabaseUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case RetryLimitExceededException:
                case SocketException:
                    return true;
                case PostgresException postgres:
                    if (postgres.SqlState.StartsWith("08", StringComparison.Ordinal)
                        || UnavailableSqlStates.Contains(postgres.SqlState))
                    {
                        return true;
                    }

                    break;
                case NpgsqlException:
                    return true;
            }
        }

        return false;
    }
}
