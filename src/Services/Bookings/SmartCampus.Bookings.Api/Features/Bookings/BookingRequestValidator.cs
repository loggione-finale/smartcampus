using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Equipment;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class BookingRequestValidator(TimeProvider timeProvider)
{
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(4);

    public IReadOnlyDictionary<string, string[]> Validate(
        CreateBookingRequest request,
        string? organizerEmail = null)
    {
        var errors = new Dictionary<string, string[]>();

        var participantsValid = EquipmentRequestValidator.TryValidateParticipantEmails(
            request.ParticipantEmails,
            organizerEmail,
            out var normalizedParticipants,
            out var participantErrors);
        if (!participantsValid)
        {
            errors[nameof(request.ParticipantEmails)] = participantErrors;
        }

        var isGroupBooking = participantsValid
            ? normalizedParticipants.Count > 0
            : request.ParticipantEmails is { Count: > 0 };

        if (!EquipmentRequestValidator.TryValidateEquipmentRequests(
                request.EquipmentRequests,
                isGroupBooking,
                requireAtLeastOneItem: false,
                out _,
                out var equipmentErrors))
        {
            errors[nameof(request.EquipmentRequests)] = equipmentErrors;
        }

        if (request.SpaceId == Guid.Empty)
        {
            errors[nameof(request.SpaceId)] = ["The space is required."];
        }

        ValidateInstant(errors, nameof(request.Start), request.Start);
        ValidateInstant(errors, nameof(request.End), request.End);

        if (errors.ContainsKey(nameof(request.SpaceId))
            || errors.ContainsKey(nameof(request.Start))
            || errors.ContainsKey(nameof(request.End)))
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

        if (errors.ContainsKey(nameof(request.End)))
        {
            return errors;
        }

        var duration = request.End - request.Start;
        if (duration < MinimumDuration || duration > MaximumDuration)
        {
            errors[nameof(request.End)] = ["The duration must be between 30 minutes and 4 hours."];
        }
        else if (!BookingTimeBands.TryResolve(request.Start.UtcDateTime, request.End.UtcDateTime, out _, out _))
        {
            errors[nameof(request.Start)] =
            [
                "A booking must fit in a single time band (Europe/Rome): morning 08:00-14:00, afternoon 14:00-19:00, evening 19:00-23:00."
            ];
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
