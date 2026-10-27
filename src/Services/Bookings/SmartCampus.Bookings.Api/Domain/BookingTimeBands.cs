namespace SmartCampus.Bookings.Api.Domain;

public static class BookingTimeBands
{
    public static readonly TimeZoneInfo CampusTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    private static readonly (TimeBand Band, TimeSpan Start, TimeSpan End)[] Bands =
    [
        (TimeBand.Morning, TimeSpan.FromHours(8), TimeSpan.FromHours(14)),
        (TimeBand.Afternoon, TimeSpan.FromHours(14), TimeSpan.FromHours(19)),
        (TimeBand.Evening, TimeSpan.FromHours(19), TimeSpan.FromHours(23))
    ];

    public static bool TryResolve(DateTime startUtc, DateTime endUtc, out DateOnly date, out TimeBand band)
    {
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), CampusTimeZone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), CampusTimeZone);
        date = DateOnly.FromDateTime(start);
        band = default;

        foreach (var candidate in Bands)
        {
            if (start.TimeOfDay >= candidate.Start && start.TimeOfDay < candidate.End)
            {
                band = candidate.Band;
                return end.Date == start.Date && end.TimeOfDay > start.TimeOfDay && end.TimeOfDay <= candidate.End;
            }
        }

        return false;
    }

    public static DateTime FromCampusTime(DateTime localTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), CampusTimeZone);
}
