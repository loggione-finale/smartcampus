namespace SmartCampus.Bookings.Tests.Support;

public static class TestSlots
{
    public static DateTimeOffset Next(int hour = 10)
    {
        var day = DateTimeOffset.UtcNow.Date.AddDays(60 + Random.Shared.Next(0, 3000));
        return new DateTimeOffset(day.AddHours(hour), TimeSpan.Zero);
    }
}
