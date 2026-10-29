namespace SmartCampus.Bookings.Api.Domain;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Closed,

    /// <summary>
    /// Derived/computed temporal status (exposed in DTOs to clients such as MyBookingResponse)
    /// when a Confirmed booking has passed its end time and awaits the closing comment.
    /// It is not persisted in the relational database to ensure natural temporal consistency.
    /// </summary>
    AwaitingComment
}
