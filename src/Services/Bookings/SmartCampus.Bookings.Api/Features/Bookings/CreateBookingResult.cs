using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class CreateBookingResult
{
    public CreateBookingOutcome Outcome { get; private init; }
    public Booking? Booking { get; private init; }
    public string? Detail { get; private init; }
    public string? PendingReferenceCode { get; private init; }
    public int? FreeSeats { get; private init; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; private init; }

    public static CreateBookingResult Created(Booking booking) =>
        new() { Outcome = CreateBookingOutcome.Created, Booking = booking };

    public static CreateBookingResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Outcome = CreateBookingOutcome.Invalid, ValidationErrors = errors };

    public static CreateBookingResult PendingClosure(string referenceCode) =>
        new()
        {
            Outcome = CreateBookingOutcome.PendingClosure,
            PendingReferenceCode = referenceCode,
            Detail = $"Leave the closing comment for booking {referenceCode} before creating a new one."
        };

    public static CreateBookingResult SpaceNotFound() =>
        new() { Outcome = CreateBookingOutcome.SpaceNotFound, Detail = "The space does not exist." };

    public static CreateBookingResult SpaceNotBookable(string? reason) =>
        new() { Outcome = CreateBookingOutcome.SpaceNotBookable, Detail = reason ?? "The space is not bookable in that time slot." };

    public static CreateBookingResult SpaceConflict() =>
        new() { Outcome = CreateBookingOutcome.SpaceConflict, Detail = "The space is already booked in that time slot." };

    public static CreateBookingResult PersonConflict() =>
        new() { Outcome = CreateBookingOutcome.PersonConflict, Detail = "You already have a booking in that time slot." };

    public static CreateBookingResult NoSeatsLeft(int freeSeats) =>
        new()
        {
            Outcome = CreateBookingOutcome.NoSeatsLeft,
            FreeSeats = freeSeats,
            Detail = freeSeats == 0
                ? "No seats are left in that time slot."
                : $"Only {freeSeats} seats are left in that time slot."
        };

    public static CreateBookingResult SpacesUnavailable() =>
        new() { Outcome = CreateBookingOutcome.SpacesUnavailable, Detail = "The Spaces service is not reachable. Try again later." };
}
