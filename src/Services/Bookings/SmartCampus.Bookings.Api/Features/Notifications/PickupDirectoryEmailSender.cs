using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>The pickup directory is not writable or the path is invalid.</summary>
public sealed class NotificationDeliveryException(string message, Exception inner)
    : InvalidOperationException(message, inner);

/// <summary>
/// Dispatches emails by writing a .eml file into the pickup directory:
/// <see cref="SmtpClient"/> with <see cref="SmtpDeliveryMethod.SpecifiedPickupDirectory"/>
/// does not open any network connections.
/// If the pickup directory is not writable, the exception (<see cref="NotificationDeliveryException"/>)
/// bubbles up to the caller: the dispatcher marks the row as Failed and HTTP operations are unaffected.
/// </summary>
public sealed class PickupDirectoryEmailSender(IOptions<NotificationOptions> options) : INotificationSender
{
    public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        // SmtpClient requires an absolute path for the pickup directory.
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
