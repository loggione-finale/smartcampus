using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Spaces.Api.Features.Spaces;
using SmartCampus.Spaces.Api.Features.Spaces.Models;

namespace SmartCampus.Spaces.Api.Controllers;

[ApiController]
[Route("spaces")]
public class SpacesController(ISpacesService spacesService) : ControllerBase
{
    [HttpGet("{id}/bookable")]
    [ProducesResponseType(typeof(SpaceBookabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        var bookability = await spacesService.GetBookabilityAsync(id, fromUtc, toUtc, cancellationToken);
        if (bookability is null)
        {
            return NotFound();
        }

        return Ok(bookability);
    }
}
