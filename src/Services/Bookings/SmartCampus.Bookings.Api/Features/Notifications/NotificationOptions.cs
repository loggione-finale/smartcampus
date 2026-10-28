namespace SmartCampus.Bookings.Api.Features.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Cartella pickup dove <see cref="System.Net.Mail.SmtpClient"/> scrive i file .eml.
    /// In modalità pickup directory non parte alcuna mail verso un server reale.
    /// </summary>
    public string PickupDirectoryLocation { get; set; } = "mail-outbox";

    /// <summary>Mittente delle mail di notifica.</summary>
    public string FromAddress { get; set; } = "no-reply@smartcampus.test";

    /// <summary>Intervallo tra i sondaggi della coda di outbox da parte del dispatcher, in secondi.</summary>
    public int DispatchIntervalSeconds { get; set; } = 5;
}
