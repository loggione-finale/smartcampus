using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Spaces;
using SmartCampus.Spaces.Api.Features.Spaces.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceBookabilityEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SpaceBookabilityEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateTestClient(ISpacesService? spacesService = null)
    {
        var mockInitializer = Substitute.For<IMongoIndexInitializer>();
        mockInitializer.InitializeIndexesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => mockInitializer);
                if (spacesService is not null)
                {
                    services.AddScoped(_ => spacesService);
                }
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetBookable_WhenMissingQueryParameters_Returns400BadRequest()
    {
        var client = CreateTestClient();

        var response = await client.GetAsync("/spaces/space-123/bookable");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("2026-10-28T12:00:00Z", "2026-10-28T10:00:00Z")] // to < from
    [InlineData("2026-10-28T10:00:00Z", "2026-10-28T10:00:00Z")] // to == from
    [InlineData("2026-10-28T09:00:00%2B02:00", "2026-10-28T11:00:00%2B02:00")] // non-UTC offset
    [InlineData("2026-10-28T09:00:00", "2026-10-28T11:00:00")]   // missing explicit UTC offset
    [InlineData("invalid-date", "2026-10-28T11:00:00Z")]          // malformed date
    public async Task GetBookable_WhenQueryParametersAreInvalid_Returns400BadRequest(string from, string to)
    {
        var client = CreateTestClient();

        var url = $"/spaces/space-123/bookable?from={from}&to={to}";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBookable_WhenSpaceDoesNotExist_Returns404NotFound()
    {
        var mockService = Substitute.For<ISpacesService>();
        mockService.GetBookabilityAsync(
            "unknown-space",
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceBookabilityResponse?>(null));

        var client = CreateTestClient(mockService);

        var url = "/spaces/unknown-space/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBookable_WhenSpaceIsAvailable_Returns200WithContractMatchingBookings()
    {
        var mockService = Substitute.For<ISpacesService>();
        var expectedResponse = new SpaceBookabilityResponse(
            Bookable: true,
            Reason: null,
            Name: "Laboratorio Informatica 2",
            Type: "Lab",
            Building: "Edificio B",
            Capacity: 24,
            AccessMode: SpaceMode.Dedicated);

        mockService.GetBookabilityAsync(
            "3f2a9c1e-0000-4000-8000-000000000002",
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceBookabilityResponse?>(expectedResponse));

        var client = CreateTestClient(mockService);

        var url = "/spaces/3f2a9c1e-0000-4000-8000-000000000002/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("bookable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("reason").ValueKind);
        Assert.Equal("Laboratorio Informatica 2", root.GetProperty("name").GetString());
        Assert.Equal("Lab", root.GetProperty("type").GetString());
        Assert.Equal("Edificio B", root.GetProperty("building").GetString());
        Assert.Equal(24, root.GetProperty("capacity").GetInt32());
        Assert.Equal("Dedicated", root.GetProperty("accessMode").GetString());
    }

    [Fact]
    public async Task GetBookable_WhenSpaceHasMaintenance_Returns200WithBookableFalseAndReason()
    {
        var mockService = Substitute.For<ISpacesService>();
        var expectedResponse = new SpaceBookabilityResponse(
            Bookable: false,
            Reason: "Manutenzione impianto elettrico",
            Name: "Aula 205",
            Type: "Classroom",
            Building: "Edificio A",
            Capacity: 35,
            AccessMode: SpaceMode.Dedicated);

        mockService.GetBookabilityAsync(
            "space-maint",
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceBookabilityResponse?>(expectedResponse));

        var client = CreateTestClient(mockService);

        var url = "/spaces/space-maint/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("bookable").GetBoolean());
        Assert.Equal("Manutenzione impianto elettrico", root.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetBookable_WhenDatabaseIsUnreachable_Returns503ServiceUnavailable()
    {
        var mockService = Substitute.For<ISpacesService>();
        mockService.GetBookabilityAsync(
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("MongoDB server selection timed out."));

        var client = CreateTestClient(mockService);

        var url = "/spaces/space-123/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "The spaces database is temporarily unreachable.",
            problem.RootElement.GetProperty("detail").GetString());
    }
}
