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
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceUnavailabilityMongoIntegrationTests
{
    private const string TestSigningKey = "spaces-unavail-mongo-jwt-signing-key-32b!";

    [MongoIntegrationFact]
    public async Task UnavailabilityLifecycle_CreateListBookableCheckAndDelete_OnRealMongoDb()
    {
        var connectionString = Environment.GetEnvironmentVariable(MongoIntegrationFactAttribute.ConnectionStringEnvVar)!;

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Spaces", connectionString);
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        });

        using var anonymousClient = factory.CreateClient();
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
        var runTag = Guid.NewGuid().ToString("N")[..8];

        try
        {
            await dbContext.Spaces.InsertOneAsync(new Space
            {
                Id = spaceId,
                Name = $"Aula Test Indisponibilità {runTag}",
                Type = SpaceType.Classroom,
                Mode = SpaceMode.Dedicated,
                Building = "Edificio Test",
                Floor = 1,
                Capacity = 30,
                IsActive = true,
                Equipment = ["videoprojector"],
                Features = new Dictionary<string, object> { ["projector"] = true },
                Unavailabilities = [],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // 1. Add unavailability window as Admin -> 201 Created
            var createResp = await adminClient.PostAsJsonAsync($"/spaces/{spaceId}/unavailabilities", new
            {
                start = "2026-10-30T08:00:00Z",
                end = "2026-10-30T12:00:00Z",
                reason = "Manutenzione straordinaria proiettore"
            });
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

            var created = await createResp.Content.ReadFromJsonAsync<UnavailabilityResponse>();
            Assert.NotNull(created);
            Assert.Equal("Manutenzione straordinaria proiettore", created!.Reason);

            // 2. Public GET /spaces/{id}/unavailabilities lists the stored window
            var listResp = await anonymousClient.GetAsync($"/spaces/{spaceId}/unavailabilities");
            Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
            var listed = await listResp.Content.ReadFromJsonAsync<List<UnavailabilityResponse>>();
            Assert.NotNull(listed);
            Assert.Single(listed!);
            Assert.Equal(created.Id, listed[0].Id);

            // 3. GET /spaces/{id}/bookable during the window returns bookable=false with the reason
            var bookableDuring = await anonymousClient.GetAsync(
                $"/spaces/{spaceId}/bookable?from=2026-10-30T09:00:00Z&to=2026-10-30T11:00:00Z");
            Assert.Equal(HttpStatusCode.OK, bookableDuring.StatusCode);
            using (var doc = JsonDocument.Parse(await bookableDuring.Content.ReadAsStringAsync()))
            {
                Assert.False(doc.RootElement.GetProperty("bookable").GetBoolean());
                Assert.Equal(
                    "Manutenzione straordinaria proiettore",
                    doc.RootElement.GetProperty("reason").GetString());
            }

            // 4. Delete the unavailability as Admin -> 204 No Content, space becomes bookable again
            var deleteResp = await adminClient.DeleteAsync($"/spaces/{spaceId}/unavailabilities/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

            var bookableAfter = await anonymousClient.GetAsync(
                $"/spaces/{spaceId}/bookable?from=2026-10-30T09:00:00Z&to=2026-10-30T11:00:00Z");
            Assert.Equal(HttpStatusCode.OK, bookableAfter.StatusCode);
            using (var docAfter = JsonDocument.Parse(await bookableAfter.Content.ReadAsStringAsync()))
            {
                Assert.True(docAfter.RootElement.GetProperty("bookable").GetBoolean());
            }
        }
        finally
        {
            await dbContext.Spaces.DeleteOneAsync(Builders<Space>.Filter.Eq(s => s.Id, spaceId));
        }
    }
}
