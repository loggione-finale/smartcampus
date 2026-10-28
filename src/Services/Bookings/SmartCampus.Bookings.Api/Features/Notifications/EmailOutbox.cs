using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

public interface IEmailOutbox
{
    Task EnqueueInvitationEmailAsync(Invitation invitation, Booking booking, CancellationToken cancellationToken = default);

    Task EnqueueInvitationExpiredEmailAsync(Invitation invitation, Booking booking, CancellationToken cancellationToken = default);
}

public sealed class EmailOutbox(BookingsDbContext dbContext, TimeProvider timeProvider) : IEmailOutbox
{
    public async Task EnqueueInvitationEmailAsync(
        Invitation invitation, Booking booking, CancellationToken cancellationToken = default)
    {
        var subject = $"Invito alla prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}";
        var body =
            $"Sei stato invitato alla prenotazione {booking.SpaceNameSnapshot} " +
            $"({booking.Period.LowerBound:dd/MM/yyyy HH:mm} - {booking.Period.UpperBound:HH:mm}). " +
            $"Conferma entro {invitation.ExpiresAt:HH:mm}.";

        dbContext.OutboxEmails.Add(new OutboxEmail
        {
            Id = Guid.NewGuid(),
            ToAddress = invitation.Email,
            Subject = subject,
            Body = body,
            Kind = "invitation",
            BookingId = booking.Id,
            Status = OutboxEmailStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueInvitationExpiredEmailAsync(
        Invitation invitation, Booking booking, CancellationToken cancellationToken = default)
    {
        var expiryEmail = BuildExpiredEmail(invitation, booking, toOrganizer: false);

        // L'email del responsabile sta nell'invito con Role == Organizer:
        // OrganizerNameSnapshot contiene il nome, non un indirizzo.
        var organizerInvitation = booking.Invitations
            .FirstOrDefault(i => i.Role == ParticipantRole.Organizer);
        if (organizerInvitation is null)
        {
            return;
        }

        var organizerEmail = BuildExpiredEmail(invitation, booking, toOrganizer: true);
        organizerEmail.ToAddress = organizerInvitation.Email;

        dbContext.OutboxEmails.AddRange(expiryEmail, organizerEmail);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private OutboxEmail BuildExpiredEmail(Invitation invitation, Booking booking, bool toOrganizer)
    {
        var subject = $"Invito scaduto - prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}";
        var body = toOrganizer
            ? $"L'invito per {invitation.Email} alla prenotazione {booking.SpaceNameSnapshot} " +
              $"({booking.Period.LowerBound:dd/MM/yyyy HH:mm} - {booking.Period.UpperBound:HH:mm}) " +
              $"è scaduto alle {invitation.ExpiresAt:HH:mm} senza conferma."
            : $"Il tuo invito alla prenotazione {booking.SpaceNameSnapshot} " +
              $"({booking.Period.LowerBound:dd/MM/yyyy HH:mm} - {booking.Period.UpperBound:HH:mm}) " +
              $"è scaduto alle {invitation.ExpiresAt:HH:mm} senza conferma.";

        return new OutboxEmail
        {
            Id = Guid.NewGuid(),
            ToAddress = invitation.Email,
            Subject = subject,
            Body = body,
            Kind = "invitation-expired",
            BookingId = booking.Id,
            Status = OutboxEmailStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
    }
}
