using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartCampus.Bookings.Api.Features.Availability;

[ApiController]
[AllowAnonymous]
[Route("bookings/availability")]
public class AvailabilityController(AvailabilityService availabilityService) : ControllerBase
{
    [HttpGet("{spaceId:guid}")]
    [ProducesResponseType<SpaceAvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAvailability(
        Guid spaceId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (!date.HasValue)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing date",
                detail: "The 'date' query parameter is required (format: yyyy-MM-dd).");
        }

        var result = await availabilityService.GetAvailabilityAsync(spaceId, date.Value, cancellationToken);

        return result.Outcome switch
        {
            AvailabilityOutcome.Success => Ok(result.Response),
            AvailabilityOutcome.SpaceNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Space not found",
                detail: result.Detail),
            AvailabilityOutcome.SpacesUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Spaces unavailable",
                detail: result.Detail),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }

    [HttpPost]
    [ProducesResponseType<IReadOnlyList<Guid>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailableSpaces(
        [FromBody] BatchAvailabilityRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing request body",
                detail: "The request body cannot be empty.");
        }

        if (request.SpaceIds is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing spaceIds",
                detail: "The 'spaceIds' list is required.");
        }

        if (request.SpaceIds.Count > 200)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Too many space IDs",
                detail: "The request cannot contain more than 200 space IDs.");
        }

        if (!request.EffectiveStart.HasValue || !request.EffectiveEnd.HasValue)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing time range",
                detail: "Both 'start' and 'end' must be specified.");
        }

        var start = request.EffectiveStart.Value;
        var end = request.EffectiveEnd.Value;

        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid time format",
                detail: "Times must be expressed in UTC (ISO 8601 with 'Z').");
        }

        if (end <= start)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid time range",
                detail: "The end time must be strictly after the start time.");
        }

        var availableSpaces = await availabilityService.GetAvailableSpacesAsync(
            request.SpaceIds, start, end, cancellationToken);

        return Ok(availableSpaces);
    }
}
