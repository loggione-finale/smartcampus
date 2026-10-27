using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceCatalogEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SpaceCatalogEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithMockService(ISpaceCatalogService catalogService) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => Substitute.For<IMongoIndexInitializer>());
                services.AddScoped(_ => catalogService);
            });
        }).CreateClient();

    [Fact]
    public async Task GetSpaces_WithCombinedFilters_Returns200AndForwardsNormalizedFilter()
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        SpaceSearchFilter? capturedFilter = null;

        mockService.SearchAsync(
            Arg.Do<SpaceSearchFilter>(f => capturedFilter = f),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpaceResponse>>(
            [
                new SpaceResponse(
                    Id: "3f2a9c1e-0000-4000-8000-000000000002",
                    Name: "Laboratorio Informatica 2",
                    Type: SpaceType.Lab,
                    Mode: SpaceMode.Dedicated,
                    Building: "Edificio B",
                    Floor: 1,
                    Capacity: 24,
                    IsActive: true,
                    Features: new Dictionary<string, object?>
                    {
                        ["workstations"] = 24,
                        ["software"] = new List<object?> { "Visual Studio Code", "Docker" }
                    },
                    Equipment: ["videoprojector", "audio-kit"],
                    Unavailabilities: [],
                    UpdatedAt: new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc))
            ]));

        using var client = CreateClientWithMockService(mockService);

        var response = await client.GetAsync(
            "/spaces?type=Lab&mode=Dedicated&minCapacity=20&equipment=videoprojector,audio-kit&software=Docker&page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(capturedFilter);
        Assert.Equal(SpaceType.Lab, capturedFilter!.Type);
        Assert.Equal(SpaceMode.Dedicated, capturedFilter.Mode);
        Assert.Equal(20, capturedFilter.MinCapacity);
        Assert.Equal(["videoprojector", "audio-kit"], capturedFilter.Equipment);
        Assert.Equal(["Docker"], capturedFilter.Software);
        Assert.Equal(1, capturedFilter.Page);
        Assert.Equal(25, capturedFilter.PageSize);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());

        var item = doc.RootElement[0];
        Assert.Equal("3f2a9c1e-0000-4000-8000-000000000002", item.GetProperty("id").GetString());
        Assert.Equal("Lab", item.GetProperty("type").GetString());
        Assert.Equal("Dedicated", item.GetProperty("mode").GetString());
        Assert.Equal(24, item.GetProperty("capacity").GetInt32());
    }

    [Fact]
    public async Task GetSpaces_WhenNoMatches_Returns200WithEmptyJsonArray()
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        mockService.SearchAsync(Arg.Any<SpaceSearchFilter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpaceResponse>>([]));

        using var client = CreateClientWithMockService(mockService);

        var response = await client.GetAsync("/spaces?type=StudyRoom&minCapacity=200");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/spaces?minCapacity=0")]
    [InlineData("/spaces?minCapacity=-10")]
    [InlineData("/spaces?minCapacity=501")]
    [InlineData("/spaces?type=InvalidType")]
    [InlineData("/spaces?type=0")]
    [InlineData("/spaces?mode=InvalidMode")]
    [InlineData("/spaces?page=0")]
    [InlineData("/spaces?pageSize=0")]
    [InlineData("/spaces?pageSize=101")]
    public async Task GetSpaces_WithInvalidQueryParameters_Returns400ProblemDetails(string url)
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        using var client = CreateClientWithMockService(mockService);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await mockService.DidNotReceive().SearchAsync(Arg.Any<SpaceSearchFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSpaces_WithOversizedTextFilter_Returns400ProblemDetails()
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        using var client = CreateClientWithMockService(mockService);

        var oversized = new string('X', 500);
        var response = await client.GetAsync($"/spaces?search={oversized}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetSpaceById_WhenSpaceExistsEvenIfInactive_Returns200Ok()
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        mockService.GetByIdAsync("3f2a9c1e-0000-4000-8000-000000000099", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(new SpaceResponse(
                Id: "3f2a9c1e-0000-4000-8000-000000000099",
                Name: "Aula Disattivata",
                Type: SpaceType.Classroom,
                Mode: SpaceMode.Dedicated,
                Building: "Edificio A",
                Floor: 2,
                Capacity: 30,
                IsActive: false,
                Features: [],
                Equipment: [],
                Unavailabilities: [],
                UpdatedAt: new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc))));

        using var client = CreateClientWithMockService(mockService);

        var response = await client.GetAsync("/spaces/3f2a9c1e-0000-4000-8000-000000000099");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal("Classroom", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetSpaceById_WhenSpaceMissing_Returns404ProblemDetails()
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        mockService.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(null));

        using var client = CreateClientWithMockService(mockService);

        var response = await client.GetAsync($"/spaces/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Endpoints_WhenDatabaseThrowsTimeoutException_Return503ProblemDetails()
    {
        var mockService = Substitute.For<ISpaceCatalogService>();
        mockService.SearchAsync(Arg.Any<SpaceSearchFilter>(), Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("MongoDB timed out"));
        mockService.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("MongoDB timed out"));

        using var client = CreateClientWithMockService(mockService);

        var searchResponse = await client.GetAsync("/spaces");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, searchResponse.StatusCode);
        Assert.Equal("application/problem+json", searchResponse.Content.Headers.ContentType?.MediaType);

        var detailResponse = await client.GetAsync($"/spaces/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, detailResponse.StatusCode);
        Assert.Equal("application/problem+json", detailResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApiEndpoint_InDevelopment_GeneratesValidDocumentWithAllSpacesRoutes()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => Substitute.For<IMongoIndexInitializer>());
                services.AddScoped(_ => Substitute.For<SmartCampus.Spaces.Api.Infrastructure.Data.ISpacesDataSeeder>());
            });
        }).CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/spaces", out _));
        Assert.True(paths.TryGetProperty("/spaces/{id}", out _));
        Assert.True(paths.TryGetProperty("/spaces/{id}/bookable", out _));
    }
}
