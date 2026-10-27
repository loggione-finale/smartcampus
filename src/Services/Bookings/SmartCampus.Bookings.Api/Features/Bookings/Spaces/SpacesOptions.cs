namespace SmartCampus.Bookings.Api.Features.Bookings.Spaces;

public class SpacesOptions
{
    public const string SectionName = "Services:Spaces";

    public string BaseUrl { get; set; } = "http://localhost:5101";
    public int TimeoutSeconds { get; set; } = 3;
}
