using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class MyBookingsEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<List<JsonElement>> GetMineAsync(HttpClient client)
    {
        var response = await client.GetAsync("/bookings/mine");
        response.EnsureSuccessStatusCode();
        var bookings = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        return bookings ?? [];
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/bookings/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Shows_the_booking_of_the_organizer_with_the_organizer_role()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);
        var start = TestSlots.Next();
        await client.PostAsJsonAsync("/bookings", new Api.Features.Bookings.CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        var bookings = await GetMineAsync(client);

        var booking = Assert.Single(bookings);
        Assert.Equal("Organizer", booking.GetProperty("role").GetString());
        Assert.Equal("Confirmed", booking.GetProperty("status").GetString());
        Assert.False(booking.GetProperty("hasPendingInvitation").GetBoolean());
    }

    [Fact]
    public async Task Does_not_show_bookings_of_other_users()
    {
        var organizer = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = TestSlots.Next();
        await organizer.PostAsJsonAsync("/bookings", new Api.Features.Bookings.CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));
        var stranger = factory.CreateAuthenticatedClient(Guid.NewGuid(), "stranger@smartcampus.test");

        var bookings = await GetMineAsync(stranger);

        Assert.Empty(bookings);
    }

    [Fact]
    public async Task Shows_future_bookings_before_past_ones()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId, "past-future@smartcampus.test");
        var futureStart = TestSlots.Next();
        await client.PostAsJsonAsync("/bookings", new Api.Features.Bookings.CreateBookingRequest(Guid.NewGuid(), futureStart, futureStart.AddHours(2)));

        var pastEnd = DateTime.UtcNow.AddDays(-5);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var past = BookingTestData.NewBooking(Guid.NewGuid(), pastEnd.AddHours(-2), pastEnd, organizerId: userId);
            past.Invitations.Add(BookingTestData.NewInvitation(past, userId, InvitationStatus.Accepted, ParticipantRole.Organizer));
            dbContext.Bookings.Add(past);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(client);

        Assert.Equal(2, bookings.Count);
        Assert.True(bookings[0].GetProperty("start").GetDateTime() > DateTime.UtcNow);
        Assert.True(bookings[1].GetProperty("start").GetDateTime() < DateTime.UtcNow);
    }

    [Fact]
    public async Task Shows_a_participant_booking_with_the_participant_role()
    {
        var participantId = Guid.NewGuid();
        var participant = factory.CreateAuthenticatedClient(participantId, "participant@smartcampus.test");
        var organizerId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var start = TestSlots.Next().UtcDateTime;
            var booking = BookingTestData.NewBooking(Guid.NewGuid(), start, start.AddHours(2), organizerId: organizerId);
            booking.Invitations.Add(BookingTestData.NewInvitation(booking, participantId, InvitationStatus.Accepted));
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(participant);

        var mine = Assert.Single(bookings);
        Assert.Equal("Participant", mine.GetProperty("role").GetString());
    }

    [Fact]
    public async Task An_anonymous_invitation_is_matched_by_email()
    {
        var email = "late-registered@smartcampus.test";
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), email);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var start = TestSlots.Next().UtcDateTime;
            var booking = BookingTestData.NewBooking(Guid.NewGuid(), start, start.AddHours(2));
            var invitation = BookingTestData.NewInvitation(booking, userId: null, InvitationStatus.Invited);
            invitation.Email = email;
            booking.Invitations.Add(invitation);
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(client);

        var mine = Assert.Single(bookings);
        Assert.Equal("Participant", mine.GetProperty("role").GetString());
        Assert.True(mine.GetProperty("hasPendingInvitation").GetBoolean());
        Assert.False(mine.GetProperty("invitationExpiresAt").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Invitations_of_other_users_do_not_match_by_email_of_another_account()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "other@smartcampus.test");
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var start = TestSlots.Next().UtcDateTime;
            var booking = BookingTestData.NewBooking(Guid.NewGuid(), start, start.AddHours(2));
            var invitation = BookingTestData.NewInvitation(booking, userId: null, InvitationStatus.Invited);
            invitation.Email = "somebody-else@smartcampus.test";
            booking.Invitations.Add(invitation);
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(client);

        Assert.Empty(bookings);
    }

    [Fact]
    public async Task Marks_a_past_confirmed_booking_without_a_closing_comment()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);
        var pastEnd = DateTime.UtcNow.AddDays(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var past = BookingTestData.NewBooking(Guid.NewGuid(), pastEnd.AddHours(-2), pastEnd, organizerId: userId);
            past.Invitations.Add(BookingTestData.NewInvitation(past, userId, InvitationStatus.Accepted, ParticipantRole.Organizer));
            dbContext.Bookings.Add(past);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(client);

        var booking = Assert.Single(bookings);
        Assert.True(booking.GetProperty("hasPendingClosureComment").GetBoolean());
    }

    [Fact]
    public async Task Does_not_mark_a_past_confirmed_booking_that_has_a_closing_comment()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);
        var pastEnd = DateTime.UtcNow.AddDays(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var past = BookingTestData.NewBooking(Guid.NewGuid(), pastEnd.AddHours(-2), pastEnd, organizerId: userId);
            past.Invitations.Add(BookingTestData.NewInvitation(past, userId, InvitationStatus.Accepted, ParticipantRole.Organizer));
            past.ClosingComment = new ClosingComment
            {
                Id = Guid.NewGuid(),
                BookingId = past.Id,
                AuthorId = userId,
                SpaceUsed = true,
                AttendeesCount = 2,
                IssuesReported = false,
                Comment = "Tutto bene, nessun problema riscontrato."
            };
            dbContext.Bookings.Add(past);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(client);

        var booking = Assert.Single(bookings);
        Assert.False(booking.GetProperty("hasPendingClosureComment").GetBoolean());
    }

    [Fact]
    public async Task Does_not_mark_a_cancelled_past_booking()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);
        var pastEnd = DateTime.UtcNow.AddDays(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var past = BookingTestData.NewBooking(Guid.NewGuid(), pastEnd.AddHours(-2), pastEnd, status: BookingStatus.Cancelled, organizerId: userId);
            past.Invitations.Add(BookingTestData.NewInvitation(past, userId, InvitationStatus.Accepted, ParticipantRole.Organizer));
            dbContext.Bookings.Add(past);
            await dbContext.SaveChangesAsync();
        }

        var bookings = await GetMineAsync(client);

        var booking = Assert.Single(bookings);
        Assert.Equal("Cancelled", booking.GetProperty("status").GetString());
        Assert.False(booking.GetProperty("hasPendingClosureComment").GetBoolean());
    }

    [Fact]
    public async Task Cancelled_bookings_stay_visible_with_the_cancelled_status()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);
        var start = TestSlots.Next();
        var created = await client.PostAsJsonAsync("/bookings", new Api.Features.Bookings.CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));
        created.EnsureSuccessStatusCode();
        var booking = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var bookingId = booking.GetProperty("id").GetGuid();
        await client.PostAsync($"/bookings/{bookingId}/cancel", null);

        var bookings = await GetMineAsync(client);

        var mine = Assert.Single(bookings);
        Assert.Equal("Cancelled", mine.GetProperty("status").GetString());
        Assert.Equal(bookingId, mine.GetProperty("id").GetGuid());
    }
}
