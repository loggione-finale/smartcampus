namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public class ClosingCommentValidator
{
    public const int MinCommentLength = 10;
    public const int MaxCommentLength = 500;

    public IReadOnlyDictionary<string, string[]> Validate(SubmitClosingCommentRequest request, int spaceCapacity)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            errors[nameof(request.Comment)] = ["The comment is required."];
        }
        else
        {
            var trimmed = request.Comment.Trim();
            if (trimmed.Length < MinCommentLength || trimmed.Length > MaxCommentLength)
            {
                errors[nameof(request.Comment)] = [$"The comment must be between {MinCommentLength} and {MaxCommentLength} characters."];
            }
        }

        if (request.AttendeesCount < 0)
        {
            errors[nameof(request.AttendeesCount)] = ["The attendees count cannot be negative."];
        }
        else if (!request.SpaceUsed && request.AttendeesCount > 0)
        {
            errors[nameof(request.AttendeesCount)] = ["The attendees count must be 0 if the space was not used."];
        }
        else if (request.AttendeesCount > spaceCapacity)
        {
            errors[nameof(request.AttendeesCount)] = [$"The attendees count cannot exceed the space capacity ({spaceCapacity})."];
        }

        return errors;
    }
}
