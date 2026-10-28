using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Admin;

public class AdminStatsEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient AdminClient() =>
        factory.CreateAuthenticatedClient(Guid.NewGuid(), "admin@smartcampus.test", SmartCampusAuthConstants.RoleAdmin);

    [Fact]
    public async Task A_User_role_gets_403()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.GetAsync("/admin/stats?from=2030-01-01&to=2030-01-02");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_reversed_range_returns_400()
    {
        var response = await AdminClient().GetAsync("/admin/stats?from=2030-01-02&to=2030-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Computes_the_stats_per_space_type_and_building_from_known_data()
    {
        var building = $"Edificio {Guid.NewGuid():N}";
        var labType = $"Lab {Guid.NewGuid():N}";
        var classroomType = $"Aula {Guid.NewGuid():N}";
        var lab = Guid.NewGuid();
        var classroom = Guid.NewGuid();
        var start = TestSlots.Next().UtcDateTime;

        // Lab: 3 closed (used with 10 attendees, used with 4 attendees and an issue, not used) + 1 cancelled.
        // Classroom: 1 confirmed, no closing comment.
        var bookings = new[]
        {
            Seed(lab, labType, building, start, BookingStatus.Closed, Comment(true, 10, false)),
            Seed(lab, labType, building, start.AddHours(1), BookingStatus.Closed, Comment(true, 4, true)),
            Seed(lab, labType, building, start.AddHours(2), BookingStatus.Closed, Comment(false, 0, false)),
            Seed(lab, labType, building, start.AddHours(3), BookingStatus.Cancelled, null),
            Seed(classroom, classroomType, building, start, BookingStatus.Confirmed, null)
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            db.Bookings.AddRange(bookings);
            await db.SaveChangesAsync();
        }
        var day = bookings[0].BookingDate.ToString("yyyy-MM-dd");
        var client = AdminClient();

        var byBuilding = await Group(client, day, "building", building);
        Assert.Equal(5, byBuilding.GetProperty("totalBookings").GetInt32());
        Assert.Equal(1, byBuilding.GetProperty("confirmed").GetInt32());
        Assert.Equal(1, byBuilding.GetProperty("cancelled").GetInt32());
        Assert.Equal(3, byBuilding.GetProperty("closed").GetInt32());
        Assert.Equal(2, byBuilding.GetProperty("spaceUsed").GetInt32());
        Assert.Equal(1, byBuilding.GetProperty("spaceNotUsed").GetInt32());
        Assert.Equal(14, byBuilding.GetProperty("totalAttendees").GetInt32());
        Assert.Equal(1, byBuilding.GetProperty("issuesReported").GetInt32());

        var bySpace = await Group(client, day, "space", lab.ToString());
        Assert.Equal(4, bySpace.GetProperty("totalBookings").GetInt32());
        Assert.Equal(0, bySpace.GetProperty("confirmed").GetInt32());
        Assert.Equal(14, bySpace.GetProperty("totalAttendees").GetInt32());

        var byType = await Group(client, day, "type", classroomType);
        Assert.Equal(1, byType.GetProperty("totalBookings").GetInt32());
        Assert.Equal(1, byType.GetProperty("confirmed").GetInt32());
        Assert.Equal(0, byType.GetProperty("spaceUsed").GetInt32());
        Assert.Equal(0, byType.GetProperty("totalAttendees").GetInt32());
    }

    private static async Task<JsonElement> Group(HttpClient client, string day, string groupBy, string key)
    {
        var groups = await client.GetFromJsonAsync<JsonElement>($"/admin/stats?from={day}&to={day}&groupBy={groupBy}", JsonOptions);
        return Assert.Single(groups.EnumerateArray(), g => g.GetProperty("key").GetString() == key);
    }

    private static Booking Seed(
        Guid spaceId, string type, string building, DateTime start, BookingStatus status, ClosingComment? comment)
    {
        var booking = BookingTestData.NewBooking(spaceId, start, start.AddHours(1), status: status);
        booking.SpaceTypeSnapshot = type;
        booking.SpaceBuildingSnapshot = building;
        if (comment is not null)
        {
            comment.BookingId = booking.Id;
            booking.ClosingComment = comment;
        }

        return booking;
    }

    private static ClosingComment Comment(bool used, int attendees, bool issues) => new()
    {
        Id = Guid.NewGuid(),
        AuthorId = Guid.NewGuid(),
        SpaceUsed = used,
        AttendeesCount = attendees,
        IssuesReported = issues,
        Comment = "Commento sintetico di prova",
        CreatedAt = DateTime.UtcNow
    };
}
