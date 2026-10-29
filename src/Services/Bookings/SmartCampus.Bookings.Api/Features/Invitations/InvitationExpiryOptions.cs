namespace SmartCampus.Bookings.Api.Features.Invitations;

public class InvitationExpiryOptions
{
    public const string SectionName = "Invitations:Expiry";

    /// <summary>Interval between scans of expired invitations, in seconds.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;
}
