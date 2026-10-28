using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Management;
using SmartCampus.Spaces.Api.Features.Management.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceManagementEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestSigningKey = "spaces-test-jwt-signing-key-at-least-32-bytes!";

    private readonly WebApplicationFactory<Program> _factory;

    public SpaceManagementEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(
        ISpaceManagementService? managementService = null,
        ISpaceCatalogService? catalogService = null,
        string? role = null,
        TimeSpan? tokenLifetime = null)
    {
        var mockMgmt = managementService ?? Substitute.For<ISpaceManagementService>();
        var mockCatalog = catalogService ?? Substitute.For<ISpaceCatalogService>();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => Substitute.For<IMongoIndexInitializer>());
                services.AddScoped(_ => mockMgmt);
                services.AddScoped(_ => mockCatalog);
            });
        }).CreateClient();

        if (!string.IsNullOrWhiteSpace(role))
        {
            var token = JwtTokenHelper.GenerateToken(
                userId: "b7e1c2d3-0000-4000-8000-000000000001",
                email: "user@smartcampus.test",
                role: role,
                signingKey: TestSigningKey,
                lifetime: tokenLifetime);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static object ValidSpacePayload(string name = "Aula 301", int capacity = 25) => new
    {
        name,
        type = "Classroom",
        mode = "Mixed",
        building = "Edificio C",
        floor = 3,
        capacity,
        equipment = new[] { "videoprojector", "interactive-whiteboard" },
        features = new Dictionary<string, object>
        {
            ["projector"] = true,
            ["movableDesks"] = true
        }
    };

    [Fact]
    public async Task WriteEndpoints_AndIncludeInactive_WithoutAuthHeader_Return401Unauthorized()
    {
        using var client = CreateClient(role: null);
        var spaceId = Guid.NewGuid().ToString("D");

        var createResp = await client.PostAsJsonAsync("/spaces", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.Unauthorized, createResp.StatusCode);

        var updateResp = await client.PutAsJsonAsync($"/spaces/{spaceId}", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.Unauthorized, updateResp.StatusCode);

        var deactivateResp = await client.PostAsync($"/spaces/{spaceId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, deactivateResp.StatusCode);

        var activateResp = await client.PostAsync($"/spaces/{spaceId}/activate", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, activateResp.StatusCode);

        var includeInactiveResp = await client.GetAsync("/spaces?includeInactive=true");
        Assert.Equal(HttpStatusCode.Unauthorized, includeInactiveResp.StatusCode);
    }

    [Fact]
    public async Task WriteEndpoints_WithExpiredAdminToken_Return401Unauthorized()
    {
        using var client = CreateClient(
            role: SmartCampusAuthConstants.RoleAdmin,
            tokenLifetime: TimeSpan.FromMinutes(-10));

        var response = await client.PostAsJsonAsync("/spaces", ValidSpacePayload());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Student")]
    public async Task WriteEndpoints_AndIncludeInactive_WithNonAdminRole_Return403Forbidden(string nonAdminRole)
    {
        var mockMgmt = Substitute.For<ISpaceManagementService>();
        using var client = CreateClient(managementService: mockMgmt, role: nonAdminRole);
        var spaceId = Guid.NewGuid().ToString("D");

        var createResp = await client.PostAsJsonAsync("/spaces", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.Forbidden, createResp.StatusCode);

        var updateResp = await client.PutAsJsonAsync($"/spaces/{spaceId}", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.Forbidden, updateResp.StatusCode);

        var deactivateResp = await client.PostAsync($"/spaces/{spaceId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, deactivateResp.StatusCode);

        var activateResp = await client.PostAsync($"/spaces/{spaceId}/activate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, activateResp.StatusCode);

        var includeInactiveResp = await client.GetAsync("/spaces?includeInactive=true");
        Assert.Equal(HttpStatusCode.Forbidden, includeInactiveResp.StatusCode);

        await mockMgmt.DidNotReceive().CreateAsync(Arg.Any<ValidatedSpaceInput>(), Arg.Any<CancellationToken>());
        await mockMgmt.DidNotReceive().UpdateAsync(Arg.Any<string>(), Arg.Any<ValidatedSpaceInput>(), Arg.Any<CancellationToken>());
        await mockMgmt.DidNotReceive().SetActiveStateAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Update_Deactivate_Activate_AndIncludeInactive_WithAdminRole_Succeed()
    {
        var createdId = "3f2a9c1e-0000-4000-8000-000000000301";
        var mockMgmt = Substitute.For<ISpaceManagementService>();
        var mockCatalog = Substitute.For<ISpaceCatalogService>();

        var sampleResponse = new SpaceResponse(
            Id: createdId,
            Name: "Aula 301",
            Type: SpaceType.Classroom,
            Mode: SpaceMode.Mixed,
            Building: "Edificio C",
            Floor: 3,
            Capacity: 25,
            IsActive: true,
            Features: new Dictionary<string, object?> { ["projector"] = true },
            Equipment: ["videoprojector", "interactive-whiteboard"],
            Unavailabilities: [],
            UpdatedAt: new DateTime(2026, 10, 27, 15, 0, 0, DateTimeKind.Utc));

        mockMgmt.CreateAsync(Arg.Any<ValidatedSpaceInput>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(sampleResponse));
        mockMgmt.UpdateAsync(createdId, Arg.Any<ValidatedSpaceInput>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(sampleResponse with { Capacity = 30 }));
        mockMgmt.SetActiveStateAsync(createdId, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(sampleResponse with { IsActive = false }));
        mockMgmt.SetActiveStateAsync(createdId, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(sampleResponse));
        mockCatalog.SearchAsync(Arg.Is<SpaceSearchFilter>(f => f.IncludeInactive), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpaceResponse>>([sampleResponse with { IsActive = false }]));

        using var adminClient = CreateClient(
            managementService: mockMgmt,
            catalogService: mockCatalog,
            role: SmartCampusAuthConstants.RoleAdmin);

        // 1. POST /spaces -> 201 Created
        var createResp = await adminClient.PostAsJsonAsync("/spaces", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        Assert.Equal($"/spaces/{createdId}", createResp.Headers.Location?.ToString());

        // 2. PUT /spaces/{id} -> 200 OK
        var updateResp = await adminClient.PutAsJsonAsync($"/spaces/{createdId}", ValidSpacePayload(capacity: 30));
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        using (var doc = JsonDocument.Parse(await updateResp.Content.ReadAsStringAsync()))
        {
            Assert.Equal(30, doc.RootElement.GetProperty("capacity").GetInt32());
        }

        // 3. POST /spaces/{id}/deactivate -> 200 OK (isActive: false)
        var deactivateResp = await adminClient.PostAsync($"/spaces/{createdId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, deactivateResp.StatusCode);
        using (var doc = JsonDocument.Parse(await deactivateResp.Content.ReadAsStringAsync()))
        {
            Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        }

        // 4. GET /spaces?includeInactive=true -> 200 OK for Admin
        var adminListResp = await adminClient.GetAsync("/spaces?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, adminListResp.StatusCode);

        // 5. POST /spaces/{id}/activate -> 200 OK (isActive: true)
        var activateResp = await adminClient.PostAsync($"/spaces/{createdId}/activate", content: null);
        Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);
        using (var doc = JsonDocument.Parse(await activateResp.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Create_WithZeroNegativeCapacityOrHugeName_Returns400ProblemDetails()
    {
        using var adminClient = CreateClient(role: SmartCampusAuthConstants.RoleAdmin);

        var zeroCapResp = await adminClient.PostAsJsonAsync("/spaces", ValidSpacePayload(capacity: 0));
        Assert.Equal(HttpStatusCode.BadRequest, zeroCapResp.StatusCode);
        Assert.Equal("application/problem+json", zeroCapResp.Content.Headers.ContentType?.MediaType);

        var negativeCapResp = await adminClient.PostAsJsonAsync("/spaces", ValidSpacePayload(capacity: -10));
        Assert.Equal(HttpStatusCode.BadRequest, negativeCapResp.StatusCode);

        var hugeNameResp = await adminClient.PostAsJsonAsync("/spaces", ValidSpacePayload(name: new string('A', 10_000)));
        Assert.Equal(HttpStatusCode.BadRequest, hugeNameResp.StatusCode);
        Assert.Equal("application/problem+json", hugeNameResp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_And_Deactivate_WhenSpaceMissing_Return404ProblemDetails()
    {
        var mockMgmt = Substitute.For<ISpaceManagementService>();
        mockMgmt.UpdateAsync(Arg.Any<string>(), Arg.Any<ValidatedSpaceInput>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(null));
        mockMgmt.SetActiveStateAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(null));

        using var adminClient = CreateClient(managementService: mockMgmt, role: SmartCampusAuthConstants.RoleAdmin);
        var missingId = Guid.NewGuid().ToString("D");

        var updateResp = await adminClient.PutAsJsonAsync($"/spaces/{missingId}", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.NotFound, updateResp.StatusCode);

        var deactivateResp = await adminClient.PostAsync($"/spaces/{missingId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.NotFound, deactivateResp.StatusCode);
    }

    [Fact]
    public async Task WriteEndpoints_WhenDatabaseUnavailable_Return503ProblemDetails()
    {
        var mockMgmt = Substitute.For<ISpaceManagementService>();
        mockMgmt.CreateAsync(Arg.Any<ValidatedSpaceInput>(), Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("MongoDB unreachable"));

        using var adminClient = CreateClient(managementService: mockMgmt, role: SmartCampusAuthConstants.RoleAdmin);

        var response = await adminClient.PostAsJsonAsync("/spaces", ValidSpacePayload());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
