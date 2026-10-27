using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceCatalogMongoIntegrationTests
{
    [MongoIntegrationFact]
    public async Task SearchAndDetail_OnRealMongoDb_VerifyCombinedFiltersAndDeactivatedSpaceBehavior()
    {
        var connectionString = Environment.GetEnvironmentVariable(MongoIntegrationFactAttribute.ConnectionStringEnvVar)!;

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Spaces", connectionString);
        });

        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ISpacesDbContext>();

        var runTag = Guid.NewGuid().ToString("N")[..8];
        var buildingTag = $"Edificio-Test-{runTag}";
        var activeLabId = Guid.NewGuid().ToString("D");
        var activeClassroomId = Guid.NewGuid().ToString("D");
        var inactiveLabId = Guid.NewGuid().ToString("D");
        var insertedIds = new[] { activeLabId, activeClassroomId, inactiveLabId };

        try
        {
            await dbContext.Spaces.InsertManyAsync(
            [
                new Space
                {
                    Id = activeLabId,
                    Name = $"Lab Avanzato {runTag}",
                    Type = SpaceType.Lab,
                    Mode = SpaceMode.Dedicated,
                    Building = buildingTag,
                    Floor = 1,
                    Capacity = 28,
                    IsActive = true,
                    Features = new Dictionary<string, object>
                    {
                        ["workstations"] = 28,
                        ["software"] = new[] { "Docker", ".NET SDK" },
                        ["projector"] = true
                    },
                    Equipment = ["videoprojector", "audio-kit"]
                },
                new Space
                {
                    Id = activeClassroomId,
                    Name = $"Aula Piccola {runTag}",
                    Type = SpaceType.Classroom,
                    Mode = SpaceMode.Mixed,
                    Building = buildingTag,
                    Floor = 0,
                    Capacity = 12,
                    IsActive = true,
                    Features = new Dictionary<string, object>
                    {
                        ["projector"] = true
                    },
                    Equipment = ["videoprojector"]
                },
                new Space
                {
                    Id = inactiveLabId,
                    Name = $"Lab Disattivato {runTag}",
                    Type = SpaceType.Lab,
                    Mode = SpaceMode.Dedicated,
                    Building = buildingTag,
                    Floor = 2,
                    Capacity = 40,
                    IsActive = false,
                    Features = new Dictionary<string, object>
                    {
                        ["workstations"] = 40,
                        ["software"] = new[] { "Docker", ".NET SDK" }
                    },
                    Equipment = ["videoprojector", "audio-kit"]
                }
            ]);

            // 1. Combined filters (type + minCapacity + equipment + software + building) return ONLY active matching lab
            var filteredResp = await client.GetAsync(
                $"/spaces?type=Lab&minCapacity=20&equipment=videoprojector,audio-kit&software=docker&building={Uri.EscapeDataString(buildingTag)}");
            Assert.Equal(HttpStatusCode.OK, filteredResp.StatusCode);

            using (var doc = JsonDocument.Parse(await filteredResp.Content.ReadAsStringAsync()))
            {
                Assert.Equal(1, doc.RootElement.GetArrayLength());
                var match = doc.RootElement[0];
                Assert.Equal(activeLabId, match.GetProperty("id").GetString());
                Assert.Equal("Lab", match.GetProperty("type").GetString());
                Assert.Equal("Dedicated", match.GetProperty("mode").GetString());
                Assert.Equal(28, match.GetProperty("features").GetProperty("workstations").GetInt32());
                Assert.Equal("Docker", match.GetProperty("features").GetProperty("software")[0].GetString());
            }

            // 2. Building search excludes deactivated space from GET /spaces
            var allInBuildingResp = await client.GetAsync(
                $"/spaces?building={Uri.EscapeDataString(buildingTag)}");
            Assert.Equal(HttpStatusCode.OK, allInBuildingResp.StatusCode);

            using (var doc = JsonDocument.Parse(await allInBuildingResp.Content.ReadAsStringAsync()))
            {
                Assert.Equal(2, doc.RootElement.GetArrayLength());
                // Ordered by Building asc, Floor asc (0 before 1), Name asc
                Assert.Equal(activeClassroomId, doc.RootElement[0].GetProperty("id").GetString());
                Assert.Equal(activeLabId, doc.RootElement[1].GetProperty("id").GetString());
            }

            // 3. Deactivated space IS still readable via GET /spaces/{id}
            var inactiveDetailResp = await client.GetAsync($"/spaces/{inactiveLabId.ToUpperInvariant()}");
            Assert.Equal(HttpStatusCode.OK, inactiveDetailResp.StatusCode);

            using (var doc = JsonDocument.Parse(await inactiveDetailResp.Content.ReadAsStringAsync()))
            {
                Assert.Equal(inactiveLabId, doc.RootElement.GetProperty("id").GetString());
                Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
                Assert.Equal("Lab", doc.RootElement.GetProperty("type").GetString());
            }
        }
        finally
        {
            await dbContext.Spaces.DeleteManyAsync(Builders<Space>.Filter.In(s => s.Id, insertedIds));
        }
    }
}
