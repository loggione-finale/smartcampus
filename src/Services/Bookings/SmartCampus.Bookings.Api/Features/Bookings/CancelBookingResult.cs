using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class CancelBookingResult
{
    public CancelBookingOutcome Outcome { get; private init; }
    public Booking? Booking { get; private init; }

    public static CancelBookingResult Cancelled(Booking booking) =>
        new() { Outcome = CancelBookingOutcome.Cancelled, Booking = booking };

    public static CancelBookingResult Failed(CancelBookingOutcome outcome) =>
        new() { Outcome = outcome };
}
