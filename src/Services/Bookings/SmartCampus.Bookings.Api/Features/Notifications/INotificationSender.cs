namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>
/// Invia le mail di notifica prodotte dagli altri moduli.
/// Implementato in modalità pickup directory: ogni invio diventa un file .eml,
/// mai una trasmissione verso un server SMTP reale.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default);
}
