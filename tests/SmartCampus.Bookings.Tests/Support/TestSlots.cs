using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Tests.Support;

public static class TestSlots
{
    public static DateTimeOffset Next(int hour = 9)
    {
        var day = DateTime.UtcNow.Date.AddDays(60 + Random.Shared.Next(0, 3000));
        var utc = BookingTimeBands.FromCampusTime(day.AddHours(hour));
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }
}
