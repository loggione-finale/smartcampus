using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>An outgoing email ready for dispatch.</summary>
public record OutgoingEmail(
    string ToAddress,
    string Subject,
    string Body,
    string Kind,
    Guid? BookingId = null);

/// <summary>
/// Email templates for the five notification events: booking created,
/// invitation to registered user, invitation to unregistered user, invitation expiry,
/// booking cancellation.
/// Times are formatted in the campus time zone (<see cref="BookingTimeBands.CampusTimeZone"/>),
/// not in UTC.
/// </summary>
public static class NotificationTemplates
{
    public static OutgoingEmail BookingCreated(Booking booking, string organizerEmail) => new(
        organizerEmail,
        $"Prenotazione creata {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        $"La tua prenotazione di {booking.SpaceNameSnapshot} " +
        $"({Period(booking)}) è stata creata con codice {booking.ReferenceCode}.",
        "booking-created",
        booking.Id);

    public static OutgoingEmail InvitationToRegistered(Invitation invitation, Booking booking, string webAppBaseUrl) => new(
        invitation.Email,
        $"Invito alla prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        $"Sei stato invitato come utente registrato alla prenotazione {booking.SpaceNameSnapshot} " +
        $"({Period(booking)}). Conferma entro {CampusTime(invitation.ExpiresAt):HH:mm}. " +
        $"Conferma qui: {webAppBaseUrl.TrimEnd('/')}/invitations/{invitation.Id}",
        "invitation",
        booking.Id);

    public static OutgoingEmail InvitationToUnregistered(Invitation invitation, Booking booking, string webAppBaseUrl) => new(
        invitation.Email,
        $"Invito alla prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        $"Sei stato invitato alla prenotazione {booking.SpaceNameSnapshot} " +
        $"({Period(booking)}). Registrati sul portale entro {CampusTime(invitation.ExpiresAt):HH:mm} " +
        $"e conferma il tuo invito. " +
        $"Registrati qui: {webAppBaseUrl.TrimEnd('/')}/?returnUrl=%2Finvitations%2F{invitation.Id}#registrazione",
        "invitation",
        booking.Id);

    public static OutgoingEmail InvitationExpired(
        Invitation invitation, Booking booking, string toAddress, bool toOrganizer) => new(
        toAddress,
        $"Invito scaduto - prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        toOrganizer
            ? $"L'invito per {invitation.Email} alla prenotazione {booking.SpaceNameSnapshot} " +
              $"({Period(booking)}) è scaduto alle {CampusTime(invitation.ExpiresAt):HH:mm} senza conferma."
            : $"Il tuo invito alla prenotazione {booking.SpaceNameSnapshot} " +
              $"({Period(booking)}) è scaduto alle {CampusTime(invitation.ExpiresAt):HH:mm} senza conferma.",
        "invitation-expired",
        booking.Id);

    public static OutgoingEmail BookingCancelled(Booking booking, string toAddress) => new(
        toAddress,
        $"Prenotazione annullata {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        $"La prenotazione di {booking.SpaceNameSnapshot} ({Period(booking)}) " +
        $"con codice {booking.ReferenceCode} è stata annullata dall'amministrazione. " +
        $"Motivo: {booking.CancellationReason}.",
        "booking-cancelled",
        booking.Id);

    private static string Period(Booking booking) =>
        $"{CampusTime(booking.Period.LowerBound):dd/MM/yyyy HH:mm} - {CampusTime(booking.Period.UpperBound):HH:mm}";

    private static DateTime CampusTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BookingTimeBands.CampusTimeZone);
}
