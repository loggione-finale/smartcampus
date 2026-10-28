using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>Una mail in uscita, pronta per l'invio.</summary>
public record OutgoingEmail(
    string ToAddress,
    string Subject,
    string Body,
    string Kind,
    Guid? BookingId = null);

/// <summary>
/// I modelli di email dei cinque eventi notificati: prenotazione creata,
/// invito a utente registrato, invito a utente non registrato, scadenza dell'invito,
/// annullamento della prenotazione.
/// I tempi sono formattati nel fuso orario del campus (<see cref="BookingTimeBands.CampusTimeZone"/>),
/// non in UTC.
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

    public static OutgoingEmail InvitationToRegistered(Invitation invitation, Booking booking) => new(
        invitation.Email,
        $"Invito alla prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        $"Sei stato invitato come utente registrato alla prenotazione {booking.SpaceNameSnapshot} " +
        $"({Period(booking)}). Conferma entro {CampusTime(invitation.ExpiresAt):HH:mm}.",
        "invitation",
        booking.Id);

    public static OutgoingEmail InvitationToUnregistered(Invitation invitation, Booking booking) => new(
        invitation.Email,
        $"Invito alla prenotazione {booking.ReferenceCode} - {booking.SpaceNameSnapshot}",
        $"Sei stato invitato alla prenotazione {booking.SpaceNameSnapshot} " +
        $"({Period(booking)}). Registrati sul portale e conferma entro {CampusTime(invitation.ExpiresAt):HH:mm}.",
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
