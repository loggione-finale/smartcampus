namespace SmartCampus.Bookings.Api.Features.Invitations;

public class InvitationExpiryOptions
{
    public const string SectionName = "Invitations:Expiry";

    /// <summary>Intervallo tra le scansioni degli inviti scaduti, in secondi.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;
}
