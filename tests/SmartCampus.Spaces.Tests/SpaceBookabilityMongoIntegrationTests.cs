using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceBookabilityMongoIntegrationTests
{
    private static string? GetTestConnectionString() =>
        Environment.GetEnvironmentVariable("ConnectionStrings__SpacesTest");

    [Fact]
    public async Task GetBookable_OnRealMongoDb_VerifiesActiveInactiveAndMaintenanceScenarios()
    {
        var connectionString = GetTestConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Spaces", connectionString);
        });

        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ISpacesDbContext>();

        var activeSpaceId = Guid.NewGuid().ToString("D");
        var inactiveSpaceId = Guid.NewGuid().ToString("D");
        var maintenanceSpaceId = Guid.NewGuid().ToString("D");
        var insertedIds = new[] { activeSpaceId, inactiveSpaceId, maintenanceSpaceId };

        try
        {
            await dbContext.Spaces.InsertManyAsync(
            [
                new Space
                {
                    Id = activeSpaceId,
                    Name = "Sala Studio Test",
                    Type = SpaceType.StudyRoom,
                    Mode = SpaceMode.Mixed,
                    Building = "Edificio A",
                    Floor = 0,
                    Capacity = 12,
                    IsActive = true
                },
                new Space
                {
                    Id = inactiveSpaceId,
                    Name = "Aula Disattivata Test",
                    Type = SpaceType.Classroom,
                    Mode = SpaceMode.Dedicated,
                    Building = "Edificio B",
                    Floor = 1,
                    Capacity = 30,
                    IsActive = false
                },
                new Space
                {
                    Id = maintenanceSpaceId,
                    Name = "Lab Manutenzione Test",
                    Type = SpaceType.Lab,
                    Mode = SpaceMode.Dedicated,
                    Building = "Edificio B",
                    Floor = 2,
                    Capacity = 24,
                    IsActive = true,
                    Unavailabilities =
                    [
                        new Unavailability
                        {
                            Start = new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc),
                            End = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc),
                            Reason = "Aggiornamento postazioni",
                            CreatedBy = "b7e1c2d3-0000-4000-8000-000000000001"
                        }
                    ]
                }
            ]);

            // 1. Active space -> bookable: true
            var activeResp = await client.GetAsync(
                $"/spaces/{activeSpaceId.ToUpperInvariant()}/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z");
            Assert.Equal(HttpStatusCode.OK, activeResp.StatusCode);
            using (var doc = JsonDocument.Parse(await activeResp.Content.ReadAsStringAsync()))
            {
                Assert.True(doc.RootElement.GetProperty("bookable").GetBoolean());
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("reason").ValueKind);
                Assert.Equal("Mixed", doc.RootElement.GetProperty("accessMode").GetString());
                Assert.Equal(12, doc.RootElement.GetProperty("capacity").GetInt32());
            }

            // 2. Inactive space -> bookable: false
            var inactiveResp = await client.GetAsync(
                $"/spaces/{inactiveSpaceId}/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z");
            Assert.Equal(HttpStatusCode.OK, inactiveResp.StatusCode);
            using (var doc = JsonDocument.Parse(await inactiveResp.Content.ReadAsStringAsync()))
            {
                Assert.False(doc.RootElement.GetProperty("bookable").GetBoolean());
                Assert.Equal("Lo spazio è disattivato", doc.RootElement.GetProperty("reason").GetString());
            }

            // 3. Maintenance overlap [09:00, 11:00) vs [08:00, 12:00) -> bookable: false
            var overlapResp = await client.GetAsync(
                $"/spaces/{maintenanceSpaceId}/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z");
            Assert.Equal(HttpStatusCode.OK, overlapResp.StatusCode);
            using (var doc = JsonDocument.Parse(await overlapResp.Content.ReadAsStringAsync()))
            {
                Assert.False(doc.RootElement.GetProperty("bookable").GetBoolean());
                Assert.Equal("Aggiornamento postazioni", doc.RootElement.GetProperty("reason").GetString());
            }

            // 4. Maintenance adjacent [12:00, 14:00) vs [08:00, 12:00) -> bookable: true
            var adjacentResp = await client.GetAsync(
                $"/spaces/{maintenanceSpaceId}/bookable?from=2026-10-28T12:00:00Z&to=2026-10-28T14:00:00Z");
            Assert.Equal(HttpStatusCode.OK, adjacentResp.StatusCode);
            using (var doc = JsonDocument.Parse(await adjacentResp.Content.ReadAsStringAsync()))
            {
                Assert.True(doc.RootElement.GetProperty("bookable").GetBoolean());
            }

            // 5. Unknown space -> 404 NotFound
            var missingResp = await client.GetAsync(
                $"/spaces/{Guid.NewGuid():D}/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z");
            Assert.Equal(HttpStatusCode.NotFound, missingResp.StatusCode);
        }
        finally
        {
            await dbContext.Spaces.DeleteManyAsync(Builders<Space>.Filter.In(s => s.Id, insertedIds));
        }
    }
}
