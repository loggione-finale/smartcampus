using System.Net.Mail;
using Microsoft.Extensions.Logging;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>
/// Enqueues notification emails as rows in <see cref="OutboxEmailStatus.Pending"/> status.
/// No disk writes here: .eml files are written by <see cref="EmailOutboxDispatcher"/>
/// outside of the HTTP request, so a pickup directory failure will not cause
/// an already saved operation to fail.
/// Enqueue methods are synchronous and do not call SaveChanges: they add entries to the change
/// tracker and the caller persists them within its operation.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>Enqueues the booking-created email to the organizer. No valid address: no email.</summary>
    void EnqueueBookingCreatedEmail(Booking booking);

    /// <summary>Enqueues an invitation email to a participant.</summary>
    void EnqueueInvitationEmail(Invitation invitation, Booking booking);

    /// <summary>
    /// Enqueues expired invitation emails: to the invitee and to the organizer
    /// (whose address is in the invitation with Role == Organizer).
    /// </summary>
    void EnqueueInvitationExpiredEmails(Invitation invitation, Booking booking);

    /// <summary>
    /// Enqueues cancellation emails: to the organizer (address in the invitation with Role == Organizer,
    /// OrganizerNameSnapshot contains the name, not an address) and to cancelled participants.
    /// </summary>
    void EnqueueBookingCancelledEmails(Booking booking);
}

public sealed class EmailOutbox(
    BookingsDbContext dbContext,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<NotificationOptions> options,
    ILogger<EmailOutbox> logger)
    : IEmailOutbox
{
    public void EnqueueBookingCreatedEmail(Booking booking)
    {
        // Organizer address is in the invitation with Role == Organizer:
        // OrganizerNameSnapshot contains the display name, not an address.
        var organizerInvitation = booking.Invitations
            .FirstOrDefault(i => i.Role == ParticipantRole.Organizer);
        if (organizerInvitation is null
            || !MailAddress.TryCreate(organizerInvitation.Email, out _))
        {
            logger.LogWarning(
                "Booking {BookingId}: no valid organizer email in the organizer invitation; skipping the booking-created email.",
                booking.Id);
            return;
        }

        Enqueue(NotificationTemplates.BookingCreated(booking, organizerInvitation.Email));
    }

    public void EnqueueInvitationEmail(Invitation invitation, Booking booking)
    {
        Enqueue(invitation.UserId is null
            ? NotificationTemplates.InvitationToUnregistered(invitation, booking, options.Value.WebAppBaseUrl)
            : NotificationTemplates.InvitationToRegistered(invitation, booking, options.Value.WebAppBaseUrl));
    }

    public void EnqueueInvitationExpiredEmails(Invitation invitation, Booking booking)
    {
        if (MailAddress.TryCreate(invitation.Email, out _))
        {
            Enqueue(
                NotificationTemplates.InvitationExpired(invitation, booking, invitation.Email, toOrganizer: false));
        }

        // Organizer email is in the invitation with Role == Organizer:
        // OrganizerNameSnapshot contains the name, not an address.
        var organizerInvitation = booking.Invitations
            .FirstOrDefault(i => i.Role == ParticipantRole.Organizer);
        if (organizerInvitation is not null && MailAddress.TryCreate(organizerInvitation.Email, out _))
        {
            Enqueue(
                NotificationTemplates.InvitationExpired(
                    invitation, booking, organizerInvitation.Email, toOrganizer: true));
        }
    }

    public void EnqueueBookingCancelledEmails(Booking booking)
    {
        // As above: the organizer recipient is the invitation with Role == Organizer.
        var organizerInvitation = booking.Invitations
            .FirstOrDefault(i => i.Role == ParticipantRole.Organizer);
        if (organizerInvitation is not null && MailAddress.TryCreate(organizerInvitation.Email, out _))
        {
            Enqueue(NotificationTemplates.BookingCancelled(booking, organizerInvitation.Email));
        }
        else
        {
            logger.LogWarning(
                "Booking {BookingId}: no valid organizer email in the organizer invitation; skipping the organizer booking-cancelled email.",
                booking.Id);
        }

        foreach (var invitation in booking.Invitations
                     .Where(i => i.Role == ParticipantRole.Participant
                         && i.Status == InvitationStatus.Cancelled
                         && MailAddress.TryCreate(i.Email, out _)))
        {
            Enqueue(NotificationTemplates.BookingCancelled(booking, invitation.Email));
        }
    }

    private void Enqueue(OutgoingEmail template)
    {
        dbContext.OutboxEmails.Add(new OutboxEmail
        {
            Id = Guid.NewGuid(),
            ToAddress = template.ToAddress,
            Subject = template.Subject,
            Body = template.Body,
            Kind = template.Kind,
            BookingId = template.BookingId,
            Status = OutboxEmailStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });
    }
}
