using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Invitations;

public class AcceptInvitationEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly DateTime Start = new(2033, 3, 30, 14, 0, 0, DateTimeKind.Utc);

    private async Task<Invitation> CreateInvitationAsync(
        Guid? userId, string email, InvitationStatus status, DateTime? expiresAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var invitation = BookingTestData.NewInvitation(booking, userId, status);
        invitation.Email = email;
        invitation.ExpiresAt = expiresAt ?? Start;
        invitation.Status = status;
        booking.Invitations.Add(invitation);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
        return invitation;
    }

    private HttpClient Client(Guid userId, string email) =>
        factory.CreateAuthenticatedClient(userId, email);

    [Fact]
    public async Task The_invited_registered_user_accepts_within_the_deadline()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var invitation = await CreateInvitationAsync(null, email, InvitationStatus.Invited);

        var response = await Client(userId, email).PostAsync(
            $"/invitations/{invitation.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var stored = await dbContext.Invitations.FindAsync([invitation.Id]);
        Assert.Equal(InvitationStatus.Accepted, stored!.Status);
        Assert.Equal(userId, stored.UserId);
        Assert.NotNull(stored.RespondedAt);
    }

    [Fact]
    public async Task After_the_deadline_the_accept_returns_conflict()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var invitation = await CreateInvitationAsync(
            null, email, InvitationStatus.Invited, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var response = await Client(userId, email).PostAsync(
            $"/invitations/{invitation.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_email_different_from_the_invited_one_returns_not_found()
    {
        var userId = Guid.NewGuid();
        var invitation = await CreateInvitationAsync(
            null, $"{Guid.NewGuid():N}@smartcampus.test", InvitationStatus.Invited);

        var response = await Client(userId, $"{Guid.NewGuid():N}@smartcampus.test").PostAsync(
            $"/invitations/{invitation.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_invitation_assigned_to_another_user_returns_forbidden()
    {
        var invitation = await CreateInvitationAsync(
            Guid.NewGuid(), $"{Guid.NewGuid():N}@smartcampus.test", InvitationStatus.Invited);

        var response = await Client(Guid.NewGuid(), invitation.Email).PostAsync(
            $"/invitations/{invitation.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_invitation_returns_not_found()
    {
        var response = await Client(Guid.NewGuid(), "someone@smartcampus.test").PostAsync(
            $"/invitations/{Guid.NewGuid()}/accept", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_confirmation_in_conflict_with_another_confirmed_booking_returns_conflict()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Accepted);
        var invitation = await CreateInvitationAsync(null, email, InvitationStatus.Invited);

        var response = await Client(userId, email).PostAsync(
            $"/invitations/{invitation.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
