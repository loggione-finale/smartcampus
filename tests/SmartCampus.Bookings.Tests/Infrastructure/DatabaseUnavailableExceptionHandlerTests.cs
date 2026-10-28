using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmartCampus.Bookings.Api.Infrastructure;

namespace SmartCampus.Bookings.Tests.Infrastructure;

public class DatabaseUnavailableExceptionHandlerTests
{
    private static PostgresException Postgres(string sqlState) =>
        new("error", "ERROR", "ERROR", sqlState);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task A_connection_failure_becomes_a_503_problem_details()
    {
        var handler = new DatabaseUnavailableExceptionHandler(NullLogger<DatabaseUnavailableExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("retries exhausted", new NpgsqlException("refused", new SocketException()));

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Database unavailable", body.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("08006")]
    [InlineData("57P01")]
    [InlineData("57P03")]
    [InlineData("53300")]
    public void Database_shutdown_and_connection_errors_count_as_unavailable(string sqlState)
    {
        Assert.True(DatabaseUnavailableExceptionHandler.IsDatabaseUnavailable(Postgres(sqlState)));
    }

    [Theory]
    [InlineData("23505")]
    [InlineData("23P01")]
    [InlineData("42P01")]
    public void Constraint_and_query_errors_are_not_treated_as_unavailable(string sqlState)
    {
        Assert.False(DatabaseUnavailableExceptionHandler.IsDatabaseUnavailable(Postgres(sqlState)));
    }

    [Fact]
    public async Task An_unrelated_exception_is_left_to_the_default_handler()
    {
        var handler = new DatabaseUnavailableExceptionHandler(NullLogger<DatabaseUnavailableExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task Sanitizes_path_with_line_breaks_in_log_warning()
    {
        var logger = new CapturingLogger<DatabaseUnavailableExceptionHandler>();
        var handler = new DatabaseUnavailableExceptionHandler(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/bookings\r\n[FORGED]";
        context.Response.Body = new MemoryStream();
        var exception = new NpgsqlException("unreachable", new SocketException());

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        var loggedMessage = Assert.Single(logger.Messages);
        Assert.Contains("/api/bookings[FORGED]", loggedMessage);
        Assert.DoesNotContain("\r", loggedMessage);
        Assert.DoesNotContain("\n", loggedMessage);
    }
}
