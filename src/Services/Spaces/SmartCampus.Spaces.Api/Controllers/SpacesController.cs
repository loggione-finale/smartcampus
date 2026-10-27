using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Features.Spaces;
using SmartCampus.Spaces.Api.Features.Spaces.Models;

namespace SmartCampus.Spaces.Api.Controllers;

[ApiController]
[Route("spaces")]
public class SpacesController(ISpacesService spacesService, ILogger<SpacesController> logger) : ControllerBase
{
    [HttpGet("{id}/bookable")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SpaceBookabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBookability(
        string id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Space id is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!from.HasValue || !to.HasValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Both 'from' and 'to' query parameters are required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var rawFrom = Request.Query["from"].ToString();
        var rawTo = Request.Query["to"].ToString();
        if (from.Value.Offset != TimeSpan.Zero ||
            to.Value.Offset != TimeSpan.Zero ||
            !HasExplicitUtcOffset(rawFrom) ||
            !HasExplicitUtcOffset(rawTo))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Times must be expressed in UTC (ISO 8601 with 'Z').",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (to.Value <= from.Value)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "The 'to' parameter must be strictly after 'from'.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var fromUtc = from.Value.UtcDateTime;
        var toUtc = to.Value.UtcDateTime;

        try
        {
            var bookability = await spacesService.GetBookabilityAsync(id, fromUtc, toUtc, cancellationToken);
            if (bookability is null)
            {
                return NotFound();
            }

            return Ok(bookability);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not verify space bookability: MongoDB is unreachable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Service Unavailable",
                Detail = "Il database degli spazi non è temporaneamente raggiungibile.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    private static bool HasExplicitUtcOffset(string? rawValue) =>
        !string.IsNullOrWhiteSpace(rawValue) &&
        (rawValue.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
         rawValue.EndsWith("+00:00", StringComparison.Ordinal) ||
         rawValue.EndsWith("-00:00", StringComparison.Ordinal));
}
