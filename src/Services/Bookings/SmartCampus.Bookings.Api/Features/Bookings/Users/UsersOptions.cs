namespace SmartCampus.Bookings.Api.Features.Bookings.Users;

public class UsersOptions
{
    public const string SectionName = "Services:Users";

    public string BaseUrl { get; set; } = "http://localhost:5103";
    public int TimeoutSeconds { get; set; } = 3;
}

