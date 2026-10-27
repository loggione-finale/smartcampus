using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class CancelBookingEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<Guid> CreateBookingAsync(HttpClient client, Guid spaceId, DateTimeOffset start)
    {
        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(2)));
        response.EnsureSuccessStatusCode();
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return booking.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await factory.CreateClient().PostAsync($"/bookings/{Guid.NewGuid()}/cancel", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_organizer_cancels_and_the_booking_keeps_the_cancelled_status()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(client, Guid.NewGuid(), TestSlots.Next());

        var response = await client.PostAsJsonAsync($"/bookings/{bookingId}/cancel", new CancelBookingRequest("Change of plans"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(bookingId, booking.GetProperty("id").GetGuid());
        Assert.Equal("Cancelled", booking.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancels_without_a_request_body()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(client, Guid.NewGuid(), TestSlots.Next());

        var response = await client.PostAsync($"/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task After_the_cancellation_the_slot_is_bookable_again_by_anyone()
    {
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();
        var organizer = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var other = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(organizer, spaceId, start);

        var blocked = await other.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(2)));
        await organizer.PostAsync($"/bookings/{bookingId}/cancel", null);
        var afterCancel = await other.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterCancel.StatusCode);
    }

    [Fact]
    public async Task After_the_cancellation_the_organizer_can_book_the_same_slot_elsewhere()
    {
        var start = TestSlots.Next();
        var organizer = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(organizer, Guid.NewGuid(), start);
        await organizer.PostAsync($"/bookings/{bookingId}/cancel", null);

        var response = await organizer.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_someone_elses_booking_returns_403()
    {
        var organizer = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var other = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(organizer, Guid.NewGuid(), TestSlots.Next());

        var response = await other.PostAsync($"/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_booking()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsync($"/bookings/{Guid.NewGuid()}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_twice_returns_409()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(client, Guid.NewGuid(), TestSlots.Next());
        await client.PostAsync($"/bookings/{bookingId}/cancel", null);

        var response = await client.PostAsync($"/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Booking already cancelled", problem!.Title);
    }

    [Fact]
    public async Task Rejects_a_reason_longer_than_500_characters()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var bookingId = await CreateBookingAsync(client, Guid.NewGuid(), TestSlots.Next());

        var response = await client.PostAsJsonAsync($"/bookings/{bookingId}/cancel", new CancelBookingRequest(new string('x', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
