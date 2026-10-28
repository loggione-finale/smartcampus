using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>
/// Invia le mail scrivendo un file .eml nella cartella pickup:
/// <see cref="SmtpClient"/> con <see cref="SmtpDeliveryMethod.SpecifiedPickupDirectory"/>
/// non apre alcuna connessione di rete.
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

        using var smtpClient = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupDirectory
        };
        smtpClient.Send(message);

        return Task.CompletedTask;
    }
}
