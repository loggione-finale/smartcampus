namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public class ClosingCommentValidator
{
    public const int MinCommentLength = 10;
    public const int MaxCommentLength = 500;

    public IReadOnlyDictionary<string, string[]> Validate(SubmitClosingCommentRequest request, int spaceCapacity)
    {
        var errors = new Dictionary<string, string[]>();

        if (!request.SpaceUsed.HasValue)
        {
            errors[nameof(request.SpaceUsed)] = ["The space used field is required."];
        }

        if (!request.AttendeesCount.HasValue)
        {
            errors[nameof(request.AttendeesCount)] = ["The attendees count is required."];
        }
        else
        {
            if (request.AttendeesCount.Value < 0)
            {
                errors[nameof(request.AttendeesCount)] = ["The attendees count cannot be negative."];
            }
            else if (request.SpaceUsed == false && request.AttendeesCount.Value > 0)
            {
                errors[nameof(request.AttendeesCount)] = ["The attendees count must be 0 if the space was not used."];
            }
            else if (request.AttendeesCount.Value > spaceCapacity)
            {
                errors[nameof(request.AttendeesCount)] = [$"The attendees count cannot exceed the space capacity ({spaceCapacity})."];
            }
        }

        if (!request.IssuesReported.HasValue)
        {
            errors[nameof(request.IssuesReported)] = ["The issues reported field is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            errors[nameof(request.Comment)] = ["The comment is required."];
        }
        else
        {
            var trimmed = request.Comment.Trim();
            // EnumerateRunes matches PostgreSQL char_length behavior with Unicode code points / emojis
            var runeCount = trimmed.EnumerateRunes().Count();
            if (runeCount < MinCommentLength || runeCount > MaxCommentLength)
            {
                errors[nameof(request.Comment)] = [$"The comment must be between {MinCommentLength} and {MaxCommentLength} characters."];
            }
        }

        return errors;
    }
}
