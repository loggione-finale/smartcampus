using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Invitations;

public class MyInvitationsEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly DateTime Start = new(2033, 4, 2, 9, 0, 0, DateTimeKind.Utc);

    private async Task CreateInvitationAsync(
        Guid? userId, string email, InvitationStatus status,
        ParticipantRole role = ParticipantRole.Participant)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var invitation = BookingTestData.NewInvitation(booking, userId, status, role);
        invitation.Email = email;
        booking.Invitations.Add(invitation);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task A_registered_user_finds_its_invitations()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Invited);
        await CreateInvitationAsync(userId, email, InvitationStatus.Accepted);

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, invitations.GetArrayLength());
    }

    [Fact]
    public async Task The_organizers_own_bookings_do_not_appear_in_their_invitations()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Accepted, ParticipantRole.Organizer);

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, invitations.GetArrayLength());
    }

    [Fact]
    public async Task An_anonymous_invitation_is_found_regardless_of_email_case()
    {
        var userId = Guid.NewGuid();
        var invitedEmail = $"Mixed.Case.{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(null, invitedEmail, InvitationStatus.Invited);

        var client = factory.CreateAuthenticatedClient(userId, invitedEmail.ToLower());
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, invitations.GetArrayLength());
    }

    [Fact]
    public async Task A_responded_invitation_does_not_appear()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Left);

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, invitations.GetArrayLength());
    }

    [Fact]
    public async Task An_expired_invitation_stays_visible_with_its_status()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Expired);

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, invitations.GetArrayLength());
        Assert.Equal(
            nameof(InvitationStatus.Expired),
            invitations[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_invitation_includes_the_booking_details()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Invited);
        await CreateInvitationAsync(null, email, InvitationStatus.Accepted);
        await CreateInvitationAsync(Guid.NewGuid(), email, InvitationStatus.Left);

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        var invitation = invitations[0];

        Assert.Equal("Aula 1", invitation.GetProperty("spaceName").GetString());
        Assert.Equal("Edificio A", invitation.GetProperty("spaceBuilding").GetString());
        Assert.Equal(
            Start,
            DateTime.Parse(invitation.GetProperty("start").GetString()!).ToUniversalTime());
        Assert.Equal(
            Start.AddHours(2),
            DateTime.Parse(invitation.GetProperty("end").GetString()!).ToUniversalTime());

        // Every invitation is created on its own booking, so the invited one
        // counts only its own participant row (the declined one is not shown).
        Assert.Equal(1, invitation.GetProperty("participantCount").GetInt32());
    }

    [Fact]
    public async Task The_participant_count_excludes_the_organizer_and_declined_invitations()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var invited = BookingTestData.NewInvitation(booking, userId, InvitationStatus.Invited);
        invited.Email = email;
        booking.Invitations.Add(invited);

        var organizer = BookingTestData.NewInvitation(booking, Guid.NewGuid(), InvitationStatus.Accepted, ParticipantRole.Organizer);
        booking.Invitations.Add(organizer);

        var declined = BookingTestData.NewInvitation(booking, Guid.NewGuid(), InvitationStatus.Left);
        booking.Invitations.Add(declined);

        var expired = BookingTestData.NewInvitation(booking, Guid.NewGuid(), InvitationStatus.Expired);
        booking.Invitations.Add(expired);


        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, invitations.GetArrayLength());

        // Only pending and accepted participants count: the organizer is not a
        // participant, while declined and expired ones are out of the booking.
        Assert.Equal(1, invitations[0].GetProperty("participantCount").GetInt32());    }
}
