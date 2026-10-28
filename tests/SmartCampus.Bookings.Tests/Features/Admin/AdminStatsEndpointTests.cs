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

    [Theory]
    [InlineData("/admin/stats")]
    [InlineData("/admin/stats?from=2030-01-01")]
    [InlineData("/admin/stats?to=2030-01-02")]
    public async Task Missing_from_or_to_parameter_returns_400(string url)
    {
        var response = await AdminClient().GetAsync(url);

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

    [Fact]
    public async Task Anonymous_access_gets_401()
    {
        var response = await factory.CreateClient().GetAsync("/admin/stats?from=2030-01-01&to=2030-01-02");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Space_grouping_uses_the_id_only_and_labels_with_the_latest_name()
    {
        var space = Guid.NewGuid();
        var start = TestSlots.Next().UtcDateTime;
        var old = Seed(space, "Aula", "Edificio X", start, BookingStatus.Confirmed, null);
        old.SpaceNameSnapshot = "Nome vecchio";
        var renamed = Seed(space, "Aula", "Edificio X", start.AddHours(1), BookingStatus.Confirmed, null);
        renamed.SpaceNameSnapshot = "Nome nuovo";
        await Save(old, renamed);
        var day = old.BookingDate.ToString("yyyy-MM-dd");

        var group = await Group(AdminClient(), day, "space", space.ToString());

        Assert.Equal(2, group.GetProperty("totalBookings").GetInt32());
        Assert.Equal("Nome nuovo", group.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Counts_only_finished_non_cancelled_bookings_without_comment()
    {
        var space = Guid.NewGuid();
        var day = DateTime.UtcNow.Date.AddDays(-1000 - Random.Shared.Next(0, 3000)).AddHours(8);
        var future = TestSlots.Next().UtcDateTime;

        // In the domain a booking only transitions to Closed when a closing comment is submitted,
        // so past bookings without comments remain Confirmed. Both past bookings without comments
        // are finished and counted under withoutComment.
        // Not counted: Closed with comment, Cancelled, and a future booking.
        await Save(
            Seed(space, "Aula", "Edificio Y", day, BookingStatus.Confirmed, null),
            Seed(space, "Aula", "Edificio Y", day.AddHours(1), BookingStatus.Confirmed, null),
            Seed(space, "Aula", "Edificio Y", day.AddHours(2), BookingStatus.Closed, Comment(true, 3, false)),
            Seed(space, "Aula", "Edificio Y", day.AddHours(3), BookingStatus.Cancelled, null),
            Seed(space, "Aula", "Edificio Y", future, BookingStatus.Confirmed, null));
        var pastDay = DateOnly.FromDateTime(day).ToString("yyyy-MM-dd");
        var futureDay = DateOnly.FromDateTime(future).ToString("yyyy-MM-dd");

        var past = await Group(AdminClient(), pastDay, "space", space.ToString());
        var upcoming = await Group(AdminClient(), futureDay, "space", space.ToString());

        Assert.Equal(4, past.GetProperty("totalBookings").GetInt32());
        Assert.Equal(2, past.GetProperty("withoutComment").GetInt32());
        Assert.Equal(0, upcoming.GetProperty("withoutComment").GetInt32());
    }

    [Fact]
    public async Task Range_is_inclusive_at_both_ends_and_groupBy_defaults_to_space()
    {
        var space = Guid.NewGuid();
        var start = TestSlots.Next().UtcDateTime;
        var first = Seed(space, "Aula", "Edificio Z", start, BookingStatus.Confirmed, null);
        var middle = Seed(space, "Aula", "Edificio Z", start.AddDays(1), BookingStatus.Confirmed, null);
        var last = Seed(space, "Aula", "Edificio Z", start.AddDays(2), BookingStatus.Confirmed, null);
        var outside = Seed(space, "Aula", "Edificio Z", start.AddDays(3), BookingStatus.Confirmed, null);
        await Save(first, middle, last, outside);

        var groups = await AdminClient().GetFromJsonAsync<JsonElement>(
            $"/admin/stats?from={first.BookingDate:yyyy-MM-dd}&to={last.BookingDate:yyyy-MM-dd}", JsonOptions);

        var group = Assert.Single(groups.EnumerateArray(), g => g.GetProperty("key").GetString() == space.ToString());
        Assert.Equal(3, group.GetProperty("totalBookings").GetInt32());
    }

    private async Task Save(params Booking[] bookings)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        db.Bookings.AddRange(bookings);
        await db.SaveChangesAsync();
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
