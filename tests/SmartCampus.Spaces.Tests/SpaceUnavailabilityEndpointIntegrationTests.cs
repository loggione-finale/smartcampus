using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Unavailabilities;
using SmartCampus.Spaces.Api.Features.Unavailabilities.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceUnavailabilityEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestSigningKey = "spaces-unavail-test-jwt-key-at-least-32-bytes!";
    private const string AdminUserId = "b7e1c2d3-0000-4000-8000-000000000001";

    private readonly WebApplicationFactory<Program> _factory;

    public SpaceUnavailabilityEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(
        ISpaceUnavailabilityService? unavailabilityService = null,
        string? role = null,
        TimeSpan? tokenLifetime = null)
    {
        var mockService = unavailabilityService ?? Substitute.For<ISpaceUnavailabilityService>();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => Substitute.For<IMongoIndexInitializer>());
                services.AddScoped(_ => mockService);
            });
        }).CreateClient();

        if (!string.IsNullOrWhiteSpace(role))
        {
            var token = JwtTokenHelper.GenerateToken(
                userId: AdminUserId,
                email: "user@smartcampus.test",
                role: role,
                signingKey: TestSigningKey,
                lifetime: tokenLifetime);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static object ValidPayload() => new
    {
        start = "2026-10-29T08:00:00Z",
        end = "2026-10-29T12:00:00Z",
        reason = "Manutenzione impianto elettrico"
    };

    [Fact]
    public async Task List_AllowsAnonymousAccess_AndReturnsChronologicalItems()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var mockService = Substitute.For<ISpaceUnavailabilityService>();
        mockService.ListBySpaceIdAsync(spaceId, null, Arg.Any<CancellationToken>())
            .Returns(
            [
                new UnavailabilityResponse(
                    "u-1",
                    new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
                    "Manutenzione impianto elettrico")
            ]);

        using var client = CreateClient(mockService, role: null);

        var response = await client.GetAsync($"/spaces/{spaceId}/unavailabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<UnavailabilityResponse>>();
        Assert.NotNull(items);
        var single = Assert.Single(items!);
        Assert.Equal("u-1", single.Id);
        Assert.Equal("Manutenzione impianto elettrico", single.Reason);
    }

    [Fact]
    public async Task WriteEndpoints_WithoutTokenOrExpiredToken_Return401Unauthorized()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var unavailabilityId = Guid.NewGuid().ToString("D");

        using var anonymousClient = CreateClient(role: null);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.PostAsJsonAsync($"/spaces/{spaceId}/unavailabilities", ValidPayload())).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.DeleteAsync($"/spaces/{spaceId}/unavailabilities/{unavailabilityId}")).StatusCode);

        using var expiredClient = CreateClient(
            role: SmartCampusAuthConstants.RoleAdmin,
            tokenLifetime: TimeSpan.FromMinutes(-5));
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await expiredClient.PostAsJsonAsync($"/spaces/{spaceId}/unavailabilities", ValidPayload())).StatusCode);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Student")]
    public async Task WriteEndpoints_WithNonAdminRole_Return403Forbidden(string role)
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var unavailabilityId = Guid.NewGuid().ToString("D");
        using var client = CreateClient(role: role);

        var postResp = await client.PostAsJsonAsync($"/spaces/{spaceId}/unavailabilities", ValidPayload());
        Assert.Equal(HttpStatusCode.Forbidden, postResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/spaces/{spaceId}/unavailabilities/{unavailabilityId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Create_WithAdminRole_PassesJwtUserIdAndReturns201Created()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var mockService = Substitute.For<ISpaceUnavailabilityService>();
        string? capturedActor = null;

        mockService.CreateAsync(
            spaceId,
            Arg.Any<ValidatedUnavailabilityInput>(),
            Arg.Do<string>(actor => capturedActor = actor),
            Arg.Any<CancellationToken>())
            .Returns(new UnavailabilityResponse(
                "8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e99",
                new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
                "Manutenzione impianto elettrico"));

        using var client = CreateClient(mockService, role: SmartCampusAuthConstants.RoleAdmin);

        var response = await client.PostAsJsonAsync($"/spaces/{spaceId}/unavailabilities", ValidPayload());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(AdminUserId, capturedActor);

        var body = await response.Content.ReadFromJsonAsync<UnavailabilityResponse>();
        Assert.NotNull(body);
        Assert.Equal("8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e99", body!.Id);
    }

    [Fact]
    public async Task Create_WhenEndBeforeStartOrInvalidPayload_Returns400BadRequest()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        using var client = CreateClient(role: SmartCampusAuthConstants.RoleAdmin);

        var invertedResp = await client.PostAsJsonAsync($"/spaces/{spaceId}/unavailabilities", new
        {
            start = "2026-10-29T12:00:00Z",
            end = "2026-10-29T08:00:00Z",
            reason = "Manutenzione"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invertedResp.StatusCode);

        var nonJsonResp = await client.PostAsync(
            $"/spaces/{spaceId}/unavailabilities",
            new StringContent("{invalid-json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, nonJsonResp.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204WhenDeleted_And404WhenNotFound()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var existingId = Guid.NewGuid().ToString("D");
        var missingId = Guid.NewGuid().ToString("D");

        var mockService = Substitute.For<ISpaceUnavailabilityService>();
        mockService.DeleteAsync(spaceId, existingId, Arg.Any<CancellationToken>())
            .Returns(DeleteUnavailabilityResult.Deleted);
        mockService.DeleteAsync(spaceId, missingId, Arg.Any<CancellationToken>())
            .Returns(DeleteUnavailabilityResult.UnavailabilityNotFound);

        using var client = CreateClient(mockService, role: SmartCampusAuthConstants.RoleAdmin);

        var okDelete = await client.DeleteAsync($"/spaces/{spaceId}/unavailabilities/{existingId}");
        Assert.Equal(HttpStatusCode.NoContent, okDelete.StatusCode);

        var notFoundDelete = await client.DeleteAsync($"/spaces/{spaceId}/unavailabilities/{missingId}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundDelete.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WhenMongoThrowsTimeout_Return503ProblemDetails()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var mockService = Substitute.For<ISpaceUnavailabilityService>();
        mockService.ListBySpaceIdAsync(spaceId, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("MongoDB timed out"));

        using var client = CreateClient(mockService, role: null);

        var response = await client.GetAsync($"/spaces/{spaceId}/unavailabilities");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Spaces database unavailable", json.RootElement.GetProperty("title").GetString());
    }
}
