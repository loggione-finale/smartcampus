using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

public interface IEmailOutbox
{
    Task EnqueueInvitationEmailAsync(Invitation invitation, Booking booking, CancellationToken cancellationToken = default);
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
        if (body.Length > 8000)
        {
            body = body[..8000];
        }

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
}
