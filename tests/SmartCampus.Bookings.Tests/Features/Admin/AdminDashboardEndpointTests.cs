using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Admin;

public class AdminDashboardEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient AdminClient() =>
        factory.CreateAuthenticatedClient(Guid.NewGuid(), "admin@smartcampus.test", SmartCampusAuthConstants.RoleAdmin);

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_User_role_gets_403()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.GetAsync("/admin/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lists_the_bookings_of_the_day_per_space_with_occupied_seats()
    {
        var start = TestSlots.Next().UtcDateTime;
        var spaceId = Guid.NewGuid();
        var booking = BookingTestData.NewBooking(spaceId, start, start.AddHours(2));
        var invitations = new[]
        {
            BookingTestData.NewInvitation(booking, Guid.NewGuid(), InvitationStatus.Accepted, ParticipantRole.Organizer),
            BookingTestData.NewInvitation(booking, null, InvitationStatus.Invited),
            BookingTestData.NewInvitation(booking, null, InvitationStatus.Expired)
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            db.Bookings.Add(booking);
            db.Invitations.AddRange(invitations);
            await db.SaveChangesAsync();
        }

        var response = await AdminClient().GetAsync($"/admin/dashboard?date={booking.BookingDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var spaces = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var space = Assert.Single(spaces.EnumerateArray());
        Assert.Equal(spaceId, space.GetProperty("spaceId").GetGuid());
        var item = Assert.Single(space.GetProperty("bookings").EnumerateArray());
        Assert.Equal(booking.ReferenceCode, item.GetProperty("referenceCode").GetString());
        Assert.Equal(2, item.GetProperty("occupiedSeats").GetInt32());
        Assert.Equal(3, item.GetProperty("participants").GetArrayLength());
    }

    [Fact]
    public async Task Filters_by_building_and_type()
    {
        var start = TestSlots.Next().UtcDateTime;
        var classroom = BookingTestData.NewBooking(Guid.NewGuid(), start, start.AddHours(1));
        var lab = BookingTestData.NewBooking(Guid.NewGuid(), start, start.AddHours(1));
        var building = $"Edificio {Guid.NewGuid():N}";
        var type = $"Type {Guid.NewGuid():N}";
        lab.SpaceBuildingSnapshot = building;
        lab.SpaceTypeSnapshot = type;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            db.Bookings.AddRange(classroom, lab);
            await db.SaveChangesAsync();
        }
        var client = AdminClient();
        var date = classroom.BookingDate.ToString("yyyy-MM-dd");

        var byBuilding = await client.GetFromJsonAsync<JsonElement>($"/admin/dashboard?date={date}&building={building}", JsonOptions);
        var byType = await client.GetFromJsonAsync<JsonElement>($"/admin/dashboard?date={date}&type={type}", JsonOptions);

        Assert.Equal(lab.SpaceId, Assert.Single(byBuilding.EnumerateArray()).GetProperty("spaceId").GetGuid());
        Assert.Equal(lab.SpaceId, Assert.Single(byType.EnumerateArray()).GetProperty("spaceId").GetGuid());
    }
}
