using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Invitations;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Invitations;

public class InvitationExpiryBackgroundServiceTests(BookingsApiFactory factory)
    : IClassFixture<BookingsApiFactory>
{
    private static readonly DateTime Now = new(2033, 3, 30, 12, 0, 0, DateTimeKind.Utc);

    private async Task<Invitation> CreateInvitationAsync(
        InvitationStatus status, DateTime? expiresAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Now.AddHours(4), Now.AddHours(6));
        var invitation = BookingTestData.NewInvitation(booking, userId: null, status);
        invitation.ExpiresAt = expiresAt ?? Now.AddHours(-1);
        invitation.Status = status;
        booking.Invitations.Add(invitation);
        var organizerInvitation = BookingTestData.NewInvitation(
            booking, booking.OrganizerId, InvitationStatus.Accepted, ParticipantRole.Organizer);
        organizerInvitation.Email = "organizer@smartcampus.test";
        booking.Invitations.Add(organizerInvitation);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
        return invitation;
    }

    private async Task RunExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var service = ActivatorUtilities.CreateInstance<InvitationExpiryBackgroundService>(
            scope.ServiceProvider,
            new FakeTimeProvider(Now),
            Microsoft.Extensions.Options.Options.Create(new InvitationExpiryOptions()));
        await service.ExpireDueInvitationsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Before_the_deadline_nothing_changes()
    {
        var invitation = await CreateInvitationAsync(
            InvitationStatus.Invited, expiresAt: Now.AddMinutes(30));

        await RunExpiryAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var stored = await dbContext.Invitations.FindAsync([invitation.Id]);
        Assert.Equal(InvitationStatus.Invited, stored!.Status);
        Assert.Null(stored.RespondedAt);
    }

    [Fact]
    public async Task After_the_deadline_the_invitation_expires_and_two_emails_are_queued()
    {
        var invitation = await CreateInvitationAsync(InvitationStatus.Invited);

        await RunExpiryAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var stored = await dbContext.Invitations.FindAsync([invitation.Id]);
        Assert.Equal(InvitationStatus.Expired, stored!.Status);
        Assert.NotNull(stored.RespondedAt);

        var emails = await dbContext.OutboxEmails
            .Where(e => e.BookingId == invitation.BookingId && e.Kind == "invitation-expired")
            .ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, e => e.ToAddress == invitation.Email);
        Assert.Contains(emails, e => e.ToAddress == "organizer@smartcampus.test");

        var booking = await dbContext.Bookings.FindAsync([invitation.BookingId]);
        Assert.Equal(BookingStatus.Confirmed, booking!.Status);
    }

    [Fact]
    public async Task A_second_run_changes_nothing()
    {
        var invitation = await CreateInvitationAsync(InvitationStatus.Invited);

        await RunExpiryAsync();
        await RunExpiryAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var stored = await dbContext.Invitations.FindAsync([invitation.Id]);
        Assert.Equal(InvitationStatus.Expired, stored!.Status);

        var emails = await dbContext.OutboxEmails
            .CountAsync(e => e.BookingId == invitation.BookingId && e.Kind == "invitation-expired");
        Assert.Equal(2, emails);
    }

    [Fact]
    public async Task Accepted_and_cancelled_invitations_are_never_touched()
    {
        var accepted = await CreateInvitationAsync(InvitationStatus.Accepted);
        var cancelled = await CreateInvitationAsync(InvitationStatus.Cancelled);

        await RunExpiryAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var storedAccepted = await dbContext.Invitations.FindAsync([accepted.Id]);
        var storedCancelled = await dbContext.Invitations.FindAsync([cancelled.Id]);
        Assert.Equal(InvitationStatus.Accepted, storedAccepted!.Status);
        Assert.Equal(InvitationStatus.Cancelled, storedCancelled!.Status);
    }
}
