using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Infrastructure;

public class DatabaseUnavailableTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(8);

    private sealed class BreakableConnectionInterceptor : DbConnectionInterceptor
    {
        public volatile bool Broken;

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            System.Data.Common.DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Broken)
            {
                throw new NpgsqlException("Connection refused", new SocketException((int)SocketError.ConnectionRefused));
            }

            return ValueTask.FromResult(result);
        }
    }

    private (HttpClient Client, BreakableConnectionInterceptor Interceptor) CreateClient()
    {
        var interceptor = new BreakableConnectionInterceptor();
        var derived = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<BookingsDbContext>(options => options.AddInterceptors(interceptor))));
        return (derived.CreateClient(), interceptor);
    }

    [Fact]
    public async Task A_request_that_needs_the_database_gets_503_with_a_readable_message_within_seconds()
    {
        var (client, interceptor) = CreateClient();
        var request = new { spaceIds = new[] { Guid.NewGuid() }, start = "2035-01-01T09:00:00Z", end = "2035-01-01T10:00:00Z" };

        var healthy = await client.PostAsJsonAsync("/bookings/availability", request);
        interceptor.Broken = true;
        var stopwatch = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/bookings/availability", request);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Database unavailable", problem.GetProperty("title").GetString());
        Assert.Contains("not reachable", problem.GetProperty("detail").GetString());
        Assert.True(stopwatch.Elapsed < MaxWait, $"answered after {stopwatch.Elapsed.TotalSeconds:F1} s");
    }

    [Fact]
    public async Task Health_reports_unhealthy_within_seconds_when_the_database_is_not_reachable()
    {
        var (client, interceptor) = CreateClient();

        var healthy = await client.GetAsync("/health");
        interceptor.Broken = true;
        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/health");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(stopwatch.Elapsed < MaxWait, $"answered after {stopwatch.Elapsed.TotalSeconds:F1} s");
    }
}
