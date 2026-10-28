using System.Net.Mail;
using Microsoft.Extensions.Logging;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>
/// Accoda le mail di notifica come righe in stato <see cref="OutboxEmailStatus.Pending"/>.
/// Nessuna scrittura su disco qui: i file .eml li scrive <see cref="EmailOutboxDispatcher"/>
/// fuori dalla richiesta HTTP, così un problema della pickup directory non fa fallire
/// un'operazione già salvata.
/// I metodi di accodamento sono sincroni e senza SaveChanges: aggiungono le righe al cambio
/// di traccia e il chiamante le salva con la sua operazione.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>Accoda la mail di prenotazione creata al responsabile. Nessun indirizzo valido: nessuna mail.</summary>
    void EnqueueBookingCreatedEmail(Booking booking);

    /// <summary>Accoda la mail di invito a un partecipante.</summary>
    void EnqueueInvitationEmail(Invitation invitation, Booking booking);

    /// <summary>
    /// Accoda le mail di invito scaduto: all'invitato e al responsabile
    /// (il cui indirizzo sta nell'invito con Role == Organizer).
    /// </summary>
    void EnqueueInvitationExpiredEmails(Invitation invitation, Booking booking);

    /// <summary>
    /// Accoda le mail di annullamento: al responsabile (indirizzo nell'invito con Role == Organizer,
    /// OrganizerNameSnapshot contiene il nome, non un indirizzo) e ai partecipanti appena annullati.
    /// </summary>
    void EnqueueBookingCancelledEmails(Booking booking);
}

public sealed class EmailOutbox(BookingsDbContext dbContext, TimeProvider timeProvider, ILogger<EmailOutbox> logger)
    : IEmailOutbox
{
    public void EnqueueBookingCreatedEmail(Booking booking)
    {
        // L'indirizzo del responsabile sta nell'invito con Role == Organizer:
        // OrganizerNameSnapshot contiene il nome visualizzato, non un indirizzo.
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
            ? NotificationTemplates.InvitationToUnregistered(invitation, booking)
            : NotificationTemplates.InvitationToRegistered(invitation, booking));
    }

    public void EnqueueInvitationExpiredEmails(Invitation invitation, Booking booking)
    {
        if (MailAddress.TryCreate(invitation.Email, out _))
        {
            Enqueue(
                NotificationTemplates.InvitationExpired(invitation, booking, invitation.Email, toOrganizer: false));
        }

        // L'email del responsabile sta nell'invito con Role == Organizer:
        // OrganizerNameSnapshot contiene il nome, non un indirizzo.
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
        // Come sopra: il destinatario del responsabile è l'invito con Role == Organizer.
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
