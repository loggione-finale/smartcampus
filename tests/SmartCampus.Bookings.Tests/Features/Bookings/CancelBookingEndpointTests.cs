using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
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
    public async Task A_participant_of_a_group_booking_cannot_cancel_it()
    {
        var organizerId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participantEmail = "participant@smartcampus.test";
        var organizer = factory.CreateAuthenticatedClient(organizerId, "organizer@smartcampus.test");

        var start = TestSlots.Next();
        var created = await organizer.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(1),
            ParticipantEmails: [participantEmail]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        Guid invitationId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var invitation = await dbContext.Invitations.SingleAsync(
                i => i.BookingId == bookingId && i.Email == participantEmail);
            invitationId = invitation.Id;
        }

        var participant = factory.CreateAuthenticatedClient(participantId, participantEmail);
        var accepted = await participant.PostAsync($"/invitations/{invitationId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var response = await participant.PostAsync($"/bookings/{bookingId}/cancel", null);

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

    [Fact]
    public async Task Cancelling_stores_who_when_and_why()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);
        var bookingId = await CreateBookingAsync(client, Guid.NewGuid(), TestSlots.Next());

        var response = await client.PostAsJsonAsync($"/bookings/{bookingId}/cancel", new CancelBookingRequest("  Cambio di programma  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(userId, booking.CancelledBy);
        Assert.NotNull(booking.CancelledAt);
        Assert.Equal("Cambio di programma", booking.CancellationReason);
    }

    [Fact]
    public async Task Simultaneous_cancellations_let_only_one_through_and_cancel_the_invitations_once()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), $"{Guid.NewGuid():N}@smartcampus.test");
        var start = TestSlots.Next();
        var created = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(2), new[] { $"{Guid.NewGuid():N}@smartcampus.test" }));
        created.EnsureSuccessStatusCode();
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsync($"/bookings/{bookingId}/cancel", null)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Invitations).SingleAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(2, booking.Invitations.Count);
        Assert.All(booking.Invitations, i => Assert.Equal(InvitationStatus.Cancelled, i.Status));
        Assert.Equal(2, await db.OutboxEmails.CountAsync(e => e.BookingId == bookingId && e.Kind == "booking-cancelled"));
    }
}
