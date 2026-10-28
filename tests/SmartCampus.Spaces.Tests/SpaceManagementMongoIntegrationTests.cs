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
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceManagementMongoIntegrationTests
{
    private const string TestSigningKey = "spaces-mongo-test-jwt-signing-key-32-bytes!";

    [MongoIntegrationFact]
    public async Task AdminLifecycle_CreateUpdateDeactivateAndReactivate_OnRealMongoDb()
    {
        var connectionString = Environment.GetEnvironmentVariable(MongoIntegrationFactAttribute.ConnectionStringEnvVar)!;

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Spaces", connectionString);
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        });

        using var anonymousClient = factory.CreateClient();
        using var studentClient = factory.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
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

        var runTag = Guid.NewGuid().ToString("N")[..8];
        var buildingTag = $"Edificio-Admin-{runTag}";
        string? createdId = null;

        try
        {
            var createPayload = new
            {
                name = $"Aula Admin {runTag}",
                type = "Classroom",
                mode = "Dedicated",
                building = buildingTag,
                floor = 1,
                capacity = 20,
                equipment = new[] { "videoprojector" },
                features = new Dictionary<string, object> { ["projector"] = true }
            };

            // 1. Student cannot create space -> 403 Forbidden
            var forbiddenResp = await studentClient.PostAsJsonAsync("/spaces", createPayload);
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

            // 2. Admin creates space -> 201 Created and immediately visible in public catalog
            var createResp = await adminClient.PostAsJsonAsync("/spaces", createPayload);
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

            using (var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync()))
            {
                createdId = doc.RootElement.GetProperty("id").GetString();
                Assert.False(string.IsNullOrWhiteSpace(createdId));
                Assert.True(doc.RootElement.GetProperty("isActive").GetBoolean());
                Assert.Equal("Dedicated", doc.RootElement.GetProperty("mode").GetString());
            }

            var publicSearchAfterCreate = await anonymousClient.GetAsync(
                $"/spaces?building={Uri.EscapeDataString(buildingTag)}");
            Assert.Equal(HttpStatusCode.OK, publicSearchAfterCreate.StatusCode);
            using (var doc = JsonDocument.Parse(await publicSearchAfterCreate.Content.ReadAsStringAsync()))
            {
                Assert.Equal(1, doc.RootElement.GetArrayLength());
                Assert.Equal(createdId, doc.RootElement[0].GetProperty("id").GetString());
            }

            // 3. Admin updates mode, capacity, equipment, and features -> 200 OK
            var updatePayload = new
            {
                name = $"Lab Misto {runTag}",
                type = "Lab",
                mode = "Mixed",
                building = buildingTag,
                floor = 2,
                capacity = 32,
                equipment = new[] { "videoprojector", "interactive-whiteboard" },
                features = new Dictionary<string, object>
                {
                    ["workstations"] = 32,
                    ["software"] = new[] { "Docker", "Blender" }
                }
            };

            var updateResp = await adminClient.PutAsJsonAsync($"/spaces/{createdId}", updatePayload);
            Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
            using (var doc = JsonDocument.Parse(await updateResp.Content.ReadAsStringAsync()))
            {
                Assert.Equal("Mixed", doc.RootElement.GetProperty("mode").GetString());
                Assert.Equal(32, doc.RootElement.GetProperty("capacity").GetInt32());
                Assert.Equal(2, doc.RootElement.GetProperty("equipment").GetArrayLength());
            }

            // 4. Admin deactivates space -> excluded from public search & not bookable, but visible to Admin
            var deactivateResp = await adminClient.PostAsync($"/spaces/{createdId}/deactivate", content: null);
            Assert.Equal(HttpStatusCode.OK, deactivateResp.StatusCode);

            var publicSearchAfterDeactivate = await anonymousClient.GetAsync(
                $"/spaces?building={Uri.EscapeDataString(buildingTag)}");
            Assert.Equal("[]", await publicSearchAfterDeactivate.Content.ReadAsStringAsync());

            var bookableAfterDeactivate = await anonymousClient.GetAsync(
                $"/spaces/{createdId}/bookable?from=2026-10-28T09:00:00Z&to=2026-10-28T11:00:00Z");
            Assert.Equal(HttpStatusCode.OK, bookableAfterDeactivate.StatusCode);
            using (var doc = JsonDocument.Parse(await bookableAfterDeactivate.Content.ReadAsStringAsync()))
            {
                Assert.False(doc.RootElement.GetProperty("bookable").GetBoolean());
            }

            var adminListWithInactive = await adminClient.GetAsync(
                $"/spaces?building={Uri.EscapeDataString(buildingTag)}&includeInactive=true");
            Assert.Equal(HttpStatusCode.OK, adminListWithInactive.StatusCode);
            using (var doc = JsonDocument.Parse(await adminListWithInactive.Content.ReadAsStringAsync()))
            {
                Assert.Equal(1, doc.RootElement.GetArrayLength());
                Assert.False(doc.RootElement[0].GetProperty("isActive").GetBoolean());
            }

            // 5. Admin reactivates space -> reappears in public search
            var activateResp = await adminClient.PostAsync($"/spaces/{createdId}/activate", content: null);
            Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);

            var publicSearchAfterReactivate = await anonymousClient.GetAsync(
                $"/spaces?building={Uri.EscapeDataString(buildingTag)}");
            using (var doc = JsonDocument.Parse(await publicSearchAfterReactivate.Content.ReadAsStringAsync()))
            {
                Assert.Equal(1, doc.RootElement.GetArrayLength());
                Assert.True(doc.RootElement[0].GetProperty("isActive").GetBoolean());
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(createdId))
            {
                await dbContext.Spaces.DeleteOneAsync(Builders<Space>.Filter.Eq(s => s.Id, createdId));
            }
        }
    }
}
