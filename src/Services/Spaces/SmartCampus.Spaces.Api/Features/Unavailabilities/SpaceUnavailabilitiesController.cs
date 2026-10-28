using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Unavailabilities.Models;

namespace SmartCampus.Spaces.Api.Features.Unavailabilities;

[ApiController]
[Route("spaces/{id}/unavailabilities")]
public sealed class SpaceUnavailabilitiesController(
    ISpaceUnavailabilityService unavailabilityService,
    ILogger<SpaceUnavailabilitiesController> logger) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<UnavailabilityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromRoute] string id,
        [FromQuery] UnavailabilityListQuery query,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(id))
        {
            return InvalidIdentifierProblem("space");
        }

        if (!query.TryNormalize(out var periodFilter, out var errorDetail))
        {
            return Problem(
                title: "Invalid unavailability query parameters",
                detail: errorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var items = await unavailabilityService.ListBySpaceIdAsync(id, periodFilter, cancellationToken);
            if (items is null)
            {
                return Problem(
                    title: "Space not found",
                    detail: $"Space '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(items);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while listing space unavailabilities.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to retrieve space unavailabilities right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost]
    [Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
    [ProducesResponseType<UnavailabilityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromRoute] string id,
        [FromBody] CreateUnavailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(id))
        {
            return InvalidIdentifierProblem("space");
        }

        if (!request.TryValidateAndNormalize(out var validated, out var errorDetail))
        {
            return Problem(
                title: "Invalid unavailability payload",
                detail: errorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var createdBy = ResolveActorId(User);

        try
        {
            var created = await unavailabilityService.CreateAsync(id, validated!, createdBy, cancellationToken);
            if (created is null)
            {
                return Problem(
                    title: "Space not found",
                    detail: $"Space '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while creating space unavailability.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to create the unavailability right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpDelete("{unavailabilityId}")]
    [Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(
        [FromRoute] string id,
        [FromRoute] string unavailabilityId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(id))
        {
            return InvalidIdentifierProblem("space");
        }

        if (!IsValidIdentifier(unavailabilityId))
        {
            return InvalidIdentifierProblem("unavailability");
        }

        try
        {
            var result = await unavailabilityService.DeleteAsync(id, unavailabilityId, cancellationToken);
            return result switch
            {
                DeleteUnavailabilityResult.Deleted => NoContent(),
                DeleteUnavailabilityResult.SpaceNotFound => Problem(
                    title: "Space not found",
                    detail: $"Space '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound),
                DeleteUnavailabilityResult.UnavailabilityNotFound => Problem(
                    title: "Unavailability not found",
                    detail: $"Unavailability '{unavailabilityId}' was not found on space '{id}'.",
                    statusCode: StatusCodes.Status404NotFound),
                _ => throw new InvalidOperationException($"Unexpected delete result: {result}")
            };
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while deleting space unavailability.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to delete the unavailability right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= SpaceSearchQuery.MaxTextFilterLength;

    private ObjectResult InvalidIdentifierProblem(string resourceName) =>
        Problem(
            title: $"Invalid {resourceName} identifier",
            detail: $"The {resourceName} identifier must be a non-empty string up to {SpaceSearchQuery.MaxTextFilterLength} characters.",
            statusCode: StatusCodes.Status400BadRequest);

    private static string ResolveActorId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub")
        ?? principal.FindFirstValue("id")
        ?? "admin";
}
