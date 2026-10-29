using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Users;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class ParticipantBandLimitTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<Booking> SeedBookingAsync(
        Guid userId,
        DateTimeOffset start,
        ParticipantRole role = ParticipantRole.Participant,
        InvitationStatus invitationStatus = InvitationStatus.Accepted,
        BookingStatus bookingStatus = BookingStatus.Confirmed)
    {
        var startUtc = start.UtcDateTime;
        var booking = BookingTestData.NewBooking(
            Guid.NewGuid(), startUtc, startUtc.AddHours(1), status: bookingStatus,
            organizerId: role == ParticipantRole.Organizer ? userId : Guid.NewGuid());
        Assert.True(BookingTimeBands.TryResolve(startUtc, startUtc.AddHours(1), out var date, out var band));
        booking.BookingDate = date;
        booking.TimeBand = band;
        booking.Invitations.Add(BookingTestData.NewInvitation(booking, userId, invitationStatus, role));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    private (Guid Id, string Email) RegisterParticipant()
    {
        var id = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        factory.Users.RegisteredUsers.Add(new RegisteredUser(id, email, "Participant"));
        return (id, email);
    }

    private async Task<HttpResponseMessage> CreateGroupAsync(DateTimeOffset start, params string[] emails)
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), $"{Guid.NewGuid():N}@smartcampus.test");
        return await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(1), ParticipantEmails: emails));
    }

    private async Task AssertTimeBandConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Time band already used", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_participant_already_confirmed_in_that_band_blocks_the_group_booking()
    {
        var (participantId, email) = RegisterParticipant();
        var start = TestSlots.Next();
        await SeedBookingAsync(participantId, start);

        var response = await CreateGroupAsync(start.AddMinutes(15), email);

        await AssertTimeBandConflictAsync(response);
    }

    [Fact]
    public async Task A_participant_who_is_an_organizer_elsewhere_in_that_band_blocks_the_group_booking()
    {
        var (participantId, email) = RegisterParticipant();
        var start = TestSlots.Next();
        await SeedBookingAsync(participantId, start, ParticipantRole.Organizer);

        var response = await CreateGroupAsync(start, email);

        await AssertTimeBandConflictAsync(response);
    }

    [Fact]
    public async Task A_blocked_group_booking_saves_nothing()
    {
        var (participantId, email) = RegisterParticipant();
        var start = TestSlots.Next();
        await SeedBookingAsync(participantId, start);
        var organizerEmail = $"{Guid.NewGuid():N}@smartcampus.test";
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId, organizerEmail);

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(1), ParticipantEmails: [email]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        Assert.False(await db.Bookings.AnyAsync(b => b.OrganizerId == organizerId));
    }

    [Fact]
    public async Task A_participant_with_only_a_pending_invitation_in_that_band_does_not_block()
    {
        var (participantId, email) = RegisterParticipant();
        var start = TestSlots.Next();
        await SeedBookingAsync(participantId, start, invitationStatus: InvitationStatus.Invited);

        var response = await CreateGroupAsync(start, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_cancelled_booking_in_that_band_does_not_block()
    {
        var (participantId, email) = RegisterParticipant();
        var start = TestSlots.Next();
        await SeedBookingAsync(participantId, start, bookingStatus: BookingStatus.Cancelled);

        var response = await CreateGroupAsync(start, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_booking_in_another_band_or_another_day_does_not_block()
    {
        var (participantId, email) = RegisterParticipant();
        var start = TestSlots.Next();
        await SeedBookingAsync(participantId, start.AddHours(6));
        await SeedBookingAsync(participantId, start.AddDays(1));

        var response = await CreateGroupAsync(start, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Participants_who_are_not_registered_are_not_checked()
    {
        var start = TestSlots.Next();

        var response = await CreateGroupAsync(start, $"{Guid.NewGuid():N}@smartcampus.test");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<Invitation> SeedPendingInvitationAsync(Guid userId, string email, DateTimeOffset start)
    {
        var booking = await SeedBookingAsync(Guid.NewGuid(), start, ParticipantRole.Organizer);
        var invitation = BookingTestData.NewInvitation(booking, userId, InvitationStatus.Invited);
        invitation.Email = email;
        invitation.ExpiresAt = DateTime.UtcNow.AddHours(1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();
        return invitation;
    }

    [Fact]
    public async Task Accepting_an_invitation_in_a_band_where_the_user_is_already_confirmed_returns_409_and_keeps_it_pending()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var start = TestSlots.Next();
        await SeedBookingAsync(userId, start);
        var invitation = await SeedPendingInvitationAsync(userId, email, start.AddHours(3));
        var client = factory.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync($"/invitations/{invitation.Id}/accept", null);

        await AssertTimeBandConflictAsync(response);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var stored = await db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(InvitationStatus.Invited, stored.Status);
    }

    [Fact]
    public async Task Accepting_an_invitation_in_a_different_band_succeeds()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var start = TestSlots.Next();
        await SeedBookingAsync(userId, start);
        var invitation = await SeedPendingInvitationAsync(userId, email, start.AddHours(6));
        var client = factory.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync($"/invitations/{invitation.Id}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Accepting_after_the_other_booking_was_cancelled_succeeds()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var start = TestSlots.Next();
        await SeedBookingAsync(userId, start, bookingStatus: BookingStatus.Cancelled);
        var invitation = await SeedPendingInvitationAsync(userId, email, start.AddHours(3));
        var client = factory.CreateAuthenticatedClient(userId, email);

        var response = await client.PostAsync($"/invitations/{invitation.Id}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
