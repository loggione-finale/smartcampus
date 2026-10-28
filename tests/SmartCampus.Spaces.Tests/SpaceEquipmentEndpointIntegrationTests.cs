using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
using SmartCampus.Spaces.Api.Features.Equipment;
using SmartCampus.Spaces.Api.Features.Equipment.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceEquipmentEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestSigningKey = "spaces-equipment-test-jwt-key-at-least-32-bytes!";
    private const string OtherSigningKey = "another-completely-different-jwt-signing-key-32b!";

    private readonly WebApplicationFactory<Program> _factory;

    public SpaceEquipmentEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(
        ISpaceEquipmentService? equipmentService = null,
        ISpaceCatalogService? catalogService = null,
        string? role = null,
        TimeSpan? tokenLifetime = null,
        string? tokenSigningKey = null)
    {
        var mockEquipment = equipmentService ?? Substitute.For<ISpaceEquipmentService>();
        mockEquipment.GetCatalog().Returns(EquipmentCatalog.All);

        var mockCatalog = catalogService ?? Substitute.For<ISpaceCatalogService>();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => Substitute.For<IMongoIndexInitializer>());
                services.AddScoped(_ => mockEquipment);
                services.AddScoped(_ => mockCatalog);
            });
        }).CreateClient();

        if (!string.IsNullOrWhiteSpace(role))
        {
            var token = JwtTokenHelper.GenerateToken(
                userId: "b7e1c2d3-0000-4000-8000-000000000001",
                email: "user@smartcampus.test",
                role: role,
                signingKey: tokenSigningKey ?? TestSigningKey,
                lifetime: tokenLifetime);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    [Theory]
    [InlineData("/equipment")]
    [InlineData("/spaces/equipment")]
    public async Task GetCatalog_AllowsAnonymousAccess_AndReturnsAllCatalogItems(string path)
    {
        using var client = CreateClient(role: null);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<EquipmentCatalogItemResponse>>();
        Assert.NotNull(items);
        Assert.Contains(items!, i => i.Code == "videoprojector" && i.Name == "Videoproiettore");
        Assert.Contains(items!, i => i.Code == "interactive-whiteboard" && i.Name == "Lavagna interattiva");
        Assert.Contains(items!, i => i.Code == "audio-kit" && i.Name == "Kit audio");
        Assert.Contains(items!, i => i.Code == "other" && i.Name == "Altro");
    }

    [Fact]
    public async Task GetSpaceById_And_GetSpaceEquipment_ReturnEquipmentPresentInSpace()
    {
        var spaceId = "3f2a9c1e-0000-4000-8000-000000000006";
        var mockCatalog = Substitute.For<ISpaceCatalogService>();
        var mockEquipment = Substitute.For<ISpaceEquipmentService>();

        var spaceResponse = SpaceResponse.From(new Space
        {
            Id = spaceId,
            Name = "Aula Mista Seminari",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Mixed,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 30,
            IsActive = true,
            Equipment = ["videoprojector", "interactive-whiteboard"],
            UpdatedAt = new DateTime(2026, 10, 27, 16, 0, 0, DateTimeKind.Utc)
        });

        mockCatalog.GetByIdAsync(spaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(spaceResponse));
        mockEquipment.GetBySpaceIdAsync(spaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EquipmentCatalogItemResponse>?>(
                EquipmentCatalog.ResolveItems(["videoprojector", "interactive-whiteboard"])));

        using var anonymousClient = CreateClient(
            equipmentService: mockEquipment,
            catalogService: mockCatalog,
            role: null);

        // 1. GET /spaces/{id} returns both equipment codes and equipmentDetails
        var detailResp = await anonymousClient.GetAsync($"/spaces/{spaceId}");
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        using (var doc = JsonDocument.Parse(await detailResp.Content.ReadAsStringAsync()))
        {
            var equipmentCodes = doc.RootElement.GetProperty("equipment")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray();
            Assert.Equal(["videoprojector", "interactive-whiteboard"], equipmentCodes);

            var details = doc.RootElement.GetProperty("equipmentDetails");
            Assert.Equal(2, details.GetArrayLength());
            Assert.Equal("Videoproiettore", details[0].GetProperty("name").GetString());
            Assert.Equal("Lavagna interattiva", details[1].GetProperty("name").GetString());
        }

        // 2. GET /spaces/{id}/equipment returns the resolved catalog items for that room
        var equipResp = await anonymousClient.GetAsync($"/spaces/{spaceId}/equipment");
        Assert.Equal(HttpStatusCode.OK, equipResp.StatusCode);
        var items = await equipResp.Content.ReadFromJsonAsync<List<EquipmentCatalogItemResponse>>();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
    }

    [Fact]
    public async Task UpdateSpaceEquipment_WithoutTokenExpiredTokenOrWrongSignature_Returns401Unauthorized()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var payload = new { equipment = new[] { "videoprojector" } };

        using var anonymousClient = CreateClient(role: null);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.PutAsJsonAsync($"/spaces/{spaceId}/equipment", payload)).StatusCode);

        using var expiredClient = CreateClient(
            role: SmartCampusAuthConstants.RoleAdmin,
            tokenLifetime: TimeSpan.FromMinutes(-5));
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await expiredClient.PutAsJsonAsync($"/spaces/{spaceId}/equipment", payload)).StatusCode);

        using var wrongKeyClient = CreateClient(
            role: SmartCampusAuthConstants.RoleAdmin,
            tokenSigningKey: OtherSigningKey);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await wrongKeyClient.PutAsJsonAsync($"/spaces/{spaceId}/equipment", payload)).StatusCode);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Student")]
    public async Task UpdateSpaceEquipment_WithNonAdminRole_Returns403Forbidden(string nonAdminRole)
    {
        var mockEquipment = Substitute.For<ISpaceEquipmentService>();
        using var client = CreateClient(equipmentService: mockEquipment, role: nonAdminRole);
        var spaceId = Guid.NewGuid().ToString("D");

        var response = await client.PutAsJsonAsync(
            $"/spaces/{spaceId}/equipment",
            new { equipment = new[] { "videoprojector", "audio-kit" } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await mockEquipment.DidNotReceive().UpdateSpaceEquipmentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSpaceEquipment_WithAdminRole_NormalizesAndReturnsUpdatedSpace()
    {
        var spaceId = "3f2a9c1e-0000-4000-8000-000000000001";
        var mockEquipment = Substitute.For<ISpaceEquipmentService>();
        IReadOnlyList<string>? capturedEquipment = null;

        var updatedResponse = SpaceResponse.From(new Space
        {
            Id = spaceId,
            Name = "Aula 101",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 30,
            IsActive = true,
            Equipment = ["videoprojector", "interactive-whiteboard", "audio-kit"],
            UpdatedAt = new DateTime(2026, 10, 27, 16, 30, 0, DateTimeKind.Utc)
        });

        mockEquipment.UpdateSpaceEquipmentAsync(
            spaceId,
            Arg.Do<IReadOnlyList<string>>(list => capturedEquipment = list),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(updatedResponse));

        using var adminClient = CreateClient(
            equipmentService: mockEquipment,
            role: SmartCampusAuthConstants.RoleAdmin);

        var response = await adminClient.PutAsJsonAsync(
            $"/spaces/{spaceId}/equipment",
            new { equipment = new[] { "Videoproiettore", "interactive-whiteboard", "Kit audio", "VIDEOPROJECTOR" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedEquipment);
        Assert.Equal(["videoprojector", "interactive-whiteboard", "audio-kit"], capturedEquipment!);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, doc.RootElement.GetProperty("equipment").GetArrayLength());
        Assert.Equal(3, doc.RootElement.GetProperty("equipmentDetails").GetArrayLength());
    }

    [Fact]
    public async Task UpdateSpaceEquipment_WithInvalidCatalogCodeMissingFieldOrMalformedJson_Returns400BadRequest()
    {
        using var adminClient = CreateClient(role: SmartCampusAuthConstants.RoleAdmin);
        var spaceId = Guid.NewGuid().ToString("D");

        // Unknown equipment not in catalog
        var unknownResp = await adminClient.PutAsJsonAsync(
            $"/spaces/{spaceId}/equipment",
            new { equipment = new[] { "laser-cutter" } });
        Assert.Equal(HttpStatusCode.BadRequest, unknownResp.StatusCode);
        Assert.Equal("application/problem+json", unknownResp.Content.Headers.ContentType?.MediaType);

        // Missing 'equipment' property
        var emptyObjResp = await adminClient.PutAsJsonAsync(
            $"/spaces/{spaceId}/equipment",
            new { });
        Assert.Equal(HttpStatusCode.BadRequest, emptyObjResp.StatusCode);

        // Malformed JSON body
        using var malformedContent = new StringContent("{not-json", Encoding.UTF8, "application/json");
        var malformedResp = await adminClient.PutAsync($"/spaces/{spaceId}/equipment", malformedContent);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResp.StatusCode);
    }

    [Fact]
    public async Task EquipmentEndpoints_WhenSpaceMissingOrDatabaseUnavailable_Return404And503()
    {
        var missingId = Guid.NewGuid().ToString("D");
        var failingId = Guid.NewGuid().ToString("D");

        var mockEquipment = Substitute.For<ISpaceEquipmentService>();
        mockEquipment.GetBySpaceIdAsync(missingId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EquipmentCatalogItemResponse>?>(null));
        mockEquipment.UpdateSpaceEquipmentAsync(missingId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpaceResponse?>(null));

        mockEquipment.GetBySpaceIdAsync(failingId, Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("MongoDB unreachable"));
        mockEquipment.UpdateSpaceEquipmentAsync(failingId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("MongoDB unreachable"));

        using var adminClient = CreateClient(
            equipmentService: mockEquipment,
            role: SmartCampusAuthConstants.RoleAdmin);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await adminClient.GetAsync($"/spaces/{missingId}/equipment")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await adminClient.PutAsJsonAsync($"/spaces/{missingId}/equipment", new { equipment = new[] { "videoprojector" } })).StatusCode);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await adminClient.GetAsync($"/spaces/{failingId}/equipment")).StatusCode);
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await adminClient.PutAsJsonAsync($"/spaces/{failingId}/equipment", new { equipment = new[] { "videoprojector" } })).StatusCode);
    }
}
