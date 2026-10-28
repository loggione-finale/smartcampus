using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Equipment.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceEquipmentMongoIntegrationTests
{
    private const string TestSigningKey = "spaces-equipment-mongo-jwt-signing-key-32b!";

    [MongoIntegrationFact]
    public async Task EquipmentLifecycle_CatalogAdminUpdateAndGetSpaceById_OnRealMongoDb()
    {
        var connectionString = Environment.GetEnvironmentVariable(MongoIntegrationFactAttribute.ConnectionStringEnvVar)!;

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Spaces", connectionString);
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        });

        using var anonymousClient = factory.CreateClient();
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.GenerateToken(
                Guid.NewGuid().ToString("D"),
                "student@smartcampus.test",
                SmartCampusAuthConstants.RoleUser,
                TestSigningKey));

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.GenerateToken(
                "b7e1c2d3-0000-4000-8000-000000000001",
                "admin@smartcampus.test",
                SmartCampusAuthConstants.RoleAdmin,
                TestSigningKey));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ISpacesDbContext>();

        var spaceId = Guid.NewGuid().ToString("D");
        var createdAt = new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);

        try
        {
            await dbContext.Spaces.InsertOneAsync(new Space
            {
                Id = spaceId,
                Name = "Aula Test Strumenti",
                Type = SpaceType.Classroom,
                Mode = SpaceMode.Dedicated,
                Building = "Edificio Test",
                Floor = 1,
                Capacity = 30,
                IsActive = true,
                Equipment = ["videoprojector"],
                Features = new Dictionary<string, object> { ["blackboard"] = true },
                Unavailabilities = [],
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });

            // 1. Public catalog endpoint lists all equipment items
            var catalogResp = await anonymousClient.GetAsync("/equipment");
            Assert.Equal(HttpStatusCode.OK, catalogResp.StatusCode);
            var catalog = await catalogResp.Content.ReadFromJsonAsync<List<EquipmentCatalogItemResponse>>();
            Assert.NotNull(catalog);
            Assert.True(catalog!.Count >= 4);

            // 2. User role cannot modify room equipment -> 403 Forbidden
            var forbiddenResp = await userClient.PutAsJsonAsync(
                $"/spaces/{spaceId}/equipment",
                new { equipment = new[] { "videoprojector", "audio-kit" } });
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

            // 3. Admin updates room equipment -> 200 OK
            var updateResp = await adminClient.PutAsJsonAsync(
                $"/spaces/{spaceId}/equipment",
                new { equipment = new[] { "Videoproiettore", "Lavagna interattiva", "Kit audio" } });
            Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

            // 4. Public GET /spaces/{id} returns updated equipment codes and equipmentDetails
            var spaceResp = await anonymousClient.GetAsync($"/spaces/{spaceId}");
            Assert.Equal(HttpStatusCode.OK, spaceResp.StatusCode);
            using (var doc = JsonDocument.Parse(await spaceResp.Content.ReadAsStringAsync()))
            {
                var codes = doc.RootElement.GetProperty("equipment")
                    .EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToArray();
                Assert.Equal(["videoprojector", "interactive-whiteboard", "audio-kit"], codes);
                Assert.Equal(3, doc.RootElement.GetProperty("equipmentDetails").GetArrayLength());
            }

            // 5. CreatedAt and Features remain untouched in MongoDB
            var stored = await dbContext.Spaces.Find(Builders<Space>.Filter.Eq(s => s.Id, spaceId)).FirstAsync();
            Assert.Equal(createdAt, stored.CreatedAt);
            Assert.True(stored.Features.ContainsKey("blackboard"));
        }
        finally
        {
            await dbContext.Spaces.DeleteOneAsync(Builders<Space>.Filter.Eq(s => s.Id, spaceId));
        }
    }
}
