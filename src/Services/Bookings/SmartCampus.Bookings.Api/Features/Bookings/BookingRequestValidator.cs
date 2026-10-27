namespace SmartCampus.Bookings.Api.Features.Bookings;

public class BookingRequestValidator(TimeProvider timeProvider)
{
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(15);

    public IReadOnlyDictionary<string, string[]> Validate(CreateBookingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.SpaceId == Guid.Empty)
        {
            errors[nameof(request.SpaceId)] = ["The space is required."];
        }

        ValidateInstant(errors, nameof(request.Start), request.Start);
        ValidateInstant(errors, nameof(request.End), request.End);

        if (errors.Count > 0)
        {
            return errors;
        }

        if (request.End <= request.Start)
        {
            errors[nameof(request.End)] = ["The end must be after the start."];
        }

        if (request.Start <= timeProvider.GetUtcNow())
        {
            errors[nameof(request.Start)] = ["The start must be in the future."];
        }

        return errors;
    }

    private static void ValidateInstant(Dictionary<string, string[]> errors, string field, DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            errors[field] = ["Times must be expressed in UTC (ISO 8601 with 'Z')."];
        }
        else if (value.UtcTicks % SlotLength.Ticks != 0)
        {
            errors[field] = ["Times must be a multiple of 15 minutes."];
        }
    }
}
