using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Features.Catalog.Models;

namespace SmartCampus.Spaces.Api.Features.Catalog;

[ApiController]
[AllowAnonymous]
[Route("spaces")]
public sealed class SpaceCatalogController(
    ISpaceCatalogService catalogService,
    ILogger<SpaceCatalogController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SpaceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromQuery] SpaceSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryNormalize(out var filter, out var errorDetail))
        {
            return Problem(
                title: "Invalid search parameters",
                detail: errorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var spaces = await catalogService.SearchAsync(filter!, cancellationToken);
            return Ok(spaces);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while searching spaces catalog.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to query the spaces catalog right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType<SpaceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > SpaceSearchQuery.MaxTextFilterLength)
        {
            return Problem(
                title: "Invalid space identifier",
                detail: $"The space identifier must be a non-empty string up to {SpaceSearchQuery.MaxTextFilterLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var space = await catalogService.GetByIdAsync(id, cancellationToken);
            if (space is null)
            {
                return Problem(
                    title: "Space not found",
                    detail: $"Space '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(space);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while retrieving space details.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to retrieve space details right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
