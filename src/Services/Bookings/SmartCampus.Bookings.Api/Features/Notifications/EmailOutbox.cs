using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

public interface IEmailOutbox
{
    Task EnqueueBookingCreatedEmailAsync(Booking booking, string organizerEmail, CancellationToken cancellationToken = default);

    Task EnqueueInvitationEmailAsync(Invitation invitation, Booking booking, CancellationToken cancellationToken = default);

    Task EnqueueInvitationExpiredEmailAsync(Invitation invitation, Booking booking, CancellationToken cancellationToken = default);

    Task EnqueueBookingCancelledEmailAsync(Booking booking, string toAddress, CancellationToken cancellationToken = default);
}

public sealed class EmailOutbox(BookingsDbContext dbContext, TimeProvider timeProvider, INotificationSender sender)
    : IEmailOutbox
{
    public async Task EnqueueBookingCreatedEmailAsync(
        Booking booking, string organizerEmail, CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(
            NotificationTemplates.BookingCreated(booking, organizerEmail), cancellationToken);
    }

    public async Task EnqueueInvitationEmailAsync(
        Invitation invitation, Booking booking, CancellationToken cancellationToken = default)
    {
        var template = invitation.UserId is null
            ? NotificationTemplates.InvitationToUnregistered(invitation, booking)
            : NotificationTemplates.InvitationToRegistered(invitation, booking);

        await EnqueueAsync(template, cancellationToken);
    }

    public async Task EnqueueInvitationExpiredEmailAsync(
        Invitation invitation, Booking booking, CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(
            NotificationTemplates.InvitationExpired(invitation, booking, invitation.Email, toOrganizer: false),
            cancellationToken);

        // L'email del responsabile sta nell'invito con Role == Organizer:
        // OrganizerNameSnapshot contiene il nome, non un indirizzo.
        var organizerInvitation = booking.Invitations
            .FirstOrDefault(i => i.Role == ParticipantRole.Organizer);
        if (organizerInvitation is null)
        {
            return;
        }

        await EnqueueAsync(
            NotificationTemplates.InvitationExpired(
                invitation, booking, organizerInvitation.Email, toOrganizer: true),
            cancellationToken);
    }

    public async Task EnqueueBookingCancelledEmailAsync(
        Booking booking, string toAddress, CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(
            NotificationTemplates.BookingCancelled(booking, toAddress), cancellationToken);
    }

    private async Task EnqueueAsync(OutgoingEmail template, CancellationToken cancellationToken)
    {
        var outboxEmail = new OutboxEmail
        {
            Id = Guid.NewGuid(),
            ToAddress = template.ToAddress,
            Subject = template.Subject,
            Body = template.Body,
            Kind = template.Kind,
            BookingId = template.BookingId,
            Status = OutboxEmailStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.OutboxEmails.Add(outboxEmail);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Invio in pickup directory: diventa un file .eml, mai una mail verso un server reale.
        await sender.SendAsync(
            template,
            cancellationToken);

        outboxEmail.Status = OutboxEmailStatus.Sent;
        outboxEmail.SentAt = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
