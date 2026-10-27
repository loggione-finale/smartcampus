using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class MixedSpaceSeatsTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Guid CreateMixedSpace(int capacity)
    {
        var spaceId = Guid.NewGuid();
        var previous = factory.Spaces.Behavior;
        factory.Spaces.Behavior = id => id == spaceId
            ? FakeSpacesClient.DefaultSpace() with { AccessMode = SpaceAccessMode.Mixed, Capacity = capacity }
            : previous(id);
        return spaceId;
    }

    private static CreateBookingRequest Request(Guid spaceId, DateTimeOffset start) =>
        new(spaceId, start, start.AddHours(2));

    [Fact]
    public async Task A_mixed_space_accepts_overlapping_bookings_up_to_its_capacity()
    {
        var spaceId = CreateMixedSpace(2);
        var start = TestSlots.Next();

        var first = await factory.CreateAuthenticatedClient(Guid.NewGuid()).PostAsJsonAsync("/bookings", Request(spaceId, start));
        var second = await factory.CreateAuthenticatedClient(Guid.NewGuid()).PostAsJsonAsync("/bookings", Request(spaceId, start.AddHours(1)));
        var third = await factory.CreateAuthenticatedClient(Guid.NewGuid()).PostAsJsonAsync("/bookings", Request(spaceId, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var problem = await third.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Not enough seats", problem.GetProperty("title").GetString());
        Assert.Equal(0, problem.GetProperty("freeSeats").GetInt32());
    }

    [Fact]
    public async Task Seats_are_counted_only_for_overlapping_slots()
    {
        var spaceId = CreateMixedSpace(1);
        var start = TestSlots.Next();
        await factory.CreateAuthenticatedClient(Guid.NewGuid()).PostAsJsonAsync("/bookings", Request(spaceId, start));

        var adjacent = await factory.CreateAuthenticatedClient(Guid.NewGuid())
            .PostAsJsonAsync("/bookings", Request(spaceId, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_booking_frees_its_seat()
    {
        var spaceId = CreateMixedSpace(1);
        var start = TestSlots.Next();
        var organizer = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var created = await organizer.PostAsJsonAsync("/bookings", Request(spaceId, start));
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        var other = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var full = await other.PostAsJsonAsync("/bookings", Request(spaceId, start));
        await organizer.PostAsync($"/bookings/{bookingId}/cancel", null);
        var afterCancel = await other.PostAsJsonAsync("/bookings", Request(spaceId, start));

        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterCancel.StatusCode);
    }

    [Fact]
    public async Task Simultaneous_requests_never_exceed_the_capacity()
    {
        const int capacity = 5;
        const int requests = 30;
        var spaceId = CreateMixedSpace(capacity);
        var start = TestSlots.Next();

        var responses = await Task.WhenAll(Enumerable.Range(0, requests).Select(_ =>
            factory.CreateAuthenticatedClient(Guid.NewGuid()).PostAsJsonAsync("/bookings", Request(spaceId, start))));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var seatsInDatabase = await CountOccupiedSeatsAsync(spaceId);

        Assert.True(seatsInDatabase <= capacity, $"The database holds {seatsInDatabase} occupied seats for a capacity of {capacity}.");
        Assert.True(created == seatsInDatabase, $"{created} requests were accepted but the database holds {seatsInDatabase} occupied seats: rows were removed during the test.");
        Assert.Equal(capacity, created);
        Assert.Equal(requests - capacity, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    private async Task<int> CountOccupiedSeatsAsync(Guid spaceId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        return await dbContext.Invitations.CountAsync(i =>
            i.Booking.SpaceId == spaceId
            && i.Booking.Status == BookingStatus.Confirmed
            && (i.Status == InvitationStatus.Invited || i.Status == InvitationStatus.Accepted));
    }

    [Fact]
    public async Task A_dedicated_space_still_accepts_only_one_booking_per_slot()
    {
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            factory.CreateAuthenticatedClient(Guid.NewGuid()).PostAsJsonAsync("/bookings", Request(spaceId, start))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
