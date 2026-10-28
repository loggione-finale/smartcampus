using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Equipment;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Equipment;

public class EquipmentRequestsPostgresEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Rejects_single_booking_with_equipment_with_400_and_persists_nothing()
    {
        var organizerId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(
            organizerId,
            $"single-{organizerId:N}@smartcampus.test");

        var response = await client.PostAsJsonAsync("/bookings", new
        {
            spaceId,
            start,
            end = start.AddHours(1),
            equipmentRequests = new[] { "videoprojector" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("EquipmentRequests", problem!.Errors.Keys);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        Assert.False(await dbContext.Bookings.AnyAsync(b => b.OrganizerId == organizerId));
    }

    [Fact]
    public async Task Group_booking_saves_equipment_requests_and_creates_admin_exception_when_tool_is_missing()
    {
        var organizerId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();
        var previousBehavior = factory.SpaceEquipment.Behavior;

        try
        {
            // Room only has 'videoprojector' installed; 'interactive-whiteboard' is missing!
            factory.SpaceEquipment.Behavior = id => id == spaceId
                ? new HashSet<string>(["videoprojector"], StringComparer.OrdinalIgnoreCase)
                : FakeSpaceEquipmentClient.DefaultEquipment();

            var organizerClient = factory.CreateAuthenticatedClient(
                organizerId,
                $"org-{organizerId:N}@smartcampus.test");

            var response = await organizerClient.PostAsJsonAsync("/bookings", new
            {
                spaceId,
                start,
                end = start.AddHours(2),
                participantEmails = new[]
                {
                    $"p1-{organizerId:N}@smartcampus.test",
                    $"p2-{organizerId:N}@smartcampus.test"
                },
                equipmentRequests = new object[]
                {
                    "Videoproiettore",
                    new { equipmentType = "interactive-whiteboard", notes = "Serve pennarello digitale" }
                }
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdJson = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            var bookingId = createdJson.GetProperty("id").GetGuid();
            var referenceCode = createdJson.GetProperty("referenceCode").GetString()!;

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

                var savedBooking = await dbContext.Bookings
                    .Include(b => b.EquipmentRequests)
                    .FirstAsync(b => b.Id == bookingId);

                Assert.True(savedBooking.IsGroup);
                Assert.Equal(3, savedBooking.SeatsRequested);
                Assert.Equal(2, savedBooking.EquipmentRequests.Count);

                var presentReq = Assert.Single(savedBooking.EquipmentRequests, r => r.EquipmentType == "videoprojector");
                Assert.Equal(EquipmentRequestStatus.Confirmed, presentReq.Status);

                var missingReq = Assert.Single(savedBooking.EquipmentRequests, r => r.EquipmentType == "interactive-whiteboard");
                Assert.Equal(EquipmentRequestStatus.Pending, missingReq.Status);
                Assert.Equal("Serve pennarello digitale", missingReq.Notes);

                // Admin exception created ONLY for the missing equipment
                var alerts = await dbContext.AdminAlerts
                    .Where(a => a.BookingId == bookingId && a.Kind == AdminAlertKind.EquipmentRequest)
                    .ToListAsync();

                var alert = Assert.Single(alerts);
                Assert.Equal(AdminAlertStatus.Open, alert.Status);
                Assert.Contains(referenceCode, alert.Message);
                Assert.Contains("Lavagna interattiva", alert.Message);
            }

            // Organizer and Admin can inspect GET /bookings/{id}/equipment; unrelated user gets 403
            var listResp = await organizerClient.GetAsync($"/bookings/{bookingId}/equipment");
            Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
            var items = await listResp.Content.ReadFromJsonAsync<List<EquipmentRequestResponse>>(JsonOptions);
            Assert.NotNull(items);
            Assert.Equal(2, items!.Count);

            var adminClient = factory.CreateAuthenticatedClient(
                Guid.NewGuid(),
                $"admin-{organizerId:N}@smartcampus.test",
                SmartCampusAuthConstants.RoleAdmin);
            Assert.Equal(
                HttpStatusCode.OK,
                (await adminClient.GetAsync($"/bookings/{bookingId}/equipment")).StatusCode);

            var strangerClient = factory.CreateAuthenticatedClient(
                Guid.NewGuid(),
                $"stranger-{organizerId:N}@smartcampus.test");
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await strangerClient.GetAsync($"/bookings/{bookingId}/equipment")).StatusCode);
        }
        finally
        {
            factory.SpaceEquipment.Behavior = previousBehavior;
        }
    }

    [Fact]
    public async Task Adding_equipment_to_existing_single_booking_returns_400_while_group_booking_succeeds()
    {
        var singleOrganizerId = Guid.NewGuid();
        var start = TestSlots.Next();
        var singleClient = factory.CreateAuthenticatedClient(
            singleOrganizerId,
            $"single-add-{singleOrganizerId:N}@smartcampus.test");

        var singleBookingResp = await singleClient.PostAsJsonAsync(
            "/bookings",
            new { spaceId = Guid.NewGuid(), start, end = start.AddHours(1) });
        Assert.Equal(HttpStatusCode.Created, singleBookingResp.StatusCode);
        var singleBookingId = (await singleBookingResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        var singleEquipResp = await singleClient.PostAsJsonAsync(
            $"/bookings/{singleBookingId}/equipment",
            new { equipmentRequests = new[] { "videoprojector" } });
        Assert.Equal(HttpStatusCode.BadRequest, singleEquipResp.StatusCode);

        // Now create a group booking without initial equipment, then attach 'other' with notes
        var groupOrganizerId = Guid.NewGuid();
        var groupSpaceId = Guid.NewGuid();
        var groupStart = TestSlots.Next();
        var groupClient = factory.CreateAuthenticatedClient(
            groupOrganizerId,
            $"group-add-{groupOrganizerId:N}@smartcampus.test");

        var groupBookingResp = await groupClient.PostAsJsonAsync("/bookings", new
        {
            spaceId = groupSpaceId,
            start = groupStart,
            end = groupStart.AddHours(1),
            participantEmails = new[] { $"member-{groupOrganizerId:N}@smartcampus.test" }
        });
        Assert.Equal(HttpStatusCode.Created, groupBookingResp.StatusCode);
        var groupBookingId = (await groupBookingResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        var addOtherResp = await groupClient.PostAsJsonAsync(
            $"/bookings/{groupBookingId}/equipment",
            new
            {
                equipmentRequests = new[]
                {
                    new { equipmentType = "other", notes = "Serve webcam grandangolare" }
                }
            });
        Assert.Equal(HttpStatusCode.Created, addOtherResp.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        Assert.True(await dbContext.AdminAlerts.AnyAsync(a =>
            a.BookingId == groupBookingId
            && a.Kind == AdminAlertKind.EquipmentRequest
            && a.Status == AdminAlertStatus.Open));
    }
}
