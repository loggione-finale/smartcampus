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
}
