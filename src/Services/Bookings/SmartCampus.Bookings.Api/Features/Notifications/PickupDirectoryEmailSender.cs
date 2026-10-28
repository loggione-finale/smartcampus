using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>La pickup directory non è scrivibile o il percorso non è valido.</summary>
public sealed class NotificationDeliveryException(string message, Exception inner)
    : InvalidOperationException(message, inner);

/// <summary>
/// Invia le mail scrivendo un file .eml nella cartella pickup:
/// <see cref="SmtpClient"/> con <see cref="SmtpDeliveryMethod.SpecifiedPickupDirectory"/>
/// non apre alcuna connessione di rete.
/// Se la pickup directory non è scrivibile, l'eccezione (<see cref="NotificationDeliveryException"/>)
/// sale al chiamante: il dispatcher marca la riga come Failed e l'operazione HTTP non è affetta.
/// </summary>
public sealed class PickupDirectoryEmailSender(IOptions<NotificationOptions> options) : INotificationSender
{
    public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        // SmtpClient richiede un percorso assoluto per la pickup directory.
        var pickupDirectory = Path.GetFullPath(options.Value.PickupDirectoryLocation);
        Directory.CreateDirectory(pickupDirectory);

        using var message = new MailMessage
        {
            From = new MailAddress(options.Value.FromAddress),
            Subject = email.Subject,
            Body = email.Body,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(email.ToAddress));

        try
        {
            using var smtpClient = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = pickupDirectory
            };
            smtpClient.Send(message);
        }
        catch (IOException exception)
        {
            throw new NotificationDeliveryException(
                $"Pickup directory '{options.Value.PickupDirectoryLocation}' is not writable.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new NotificationDeliveryException(
                $"Pickup directory '{options.Value.PickupDirectoryLocation}' is not writable.", exception);
        }

        return Task.CompletedTask;
    }
}
