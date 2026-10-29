namespace SmartCampus.Bookings.Api.Features.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Pickup directory where <see cref="System.Net.Mail.SmtpClient"/> writes .eml files.
    /// In pickup directory mode no email is dispatched to an actual mail server.
    /// </summary>
    public string PickupDirectoryLocation { get; set; } = "mail-outbox";

    /// <summary>
    /// Base URL of the web portal used in links within email bodies
    /// (e.g. the invitation confirmation link).
    /// </summary>
    public string WebAppBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>Sender address for notification emails.</summary>
    public string FromAddress { get; set; } = "no-reply@smartcampus.test";

    /// <summary>Interval between outbox queue polls by the dispatcher, in seconds.</summary>
    public int DispatchIntervalSeconds { get; set; } = 5;
}
