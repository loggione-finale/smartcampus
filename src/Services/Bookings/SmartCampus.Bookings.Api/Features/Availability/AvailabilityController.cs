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
}
