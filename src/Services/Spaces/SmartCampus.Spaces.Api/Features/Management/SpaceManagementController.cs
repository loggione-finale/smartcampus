using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Management.Models;

namespace SmartCampus.Spaces.Api.Features.Management;

[ApiController]
[Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
[Route("spaces")]
public sealed class SpaceManagementController(
    ISpaceManagementService managementService,
    ILogger<SpaceManagementController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SpaceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSpaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidateAndNormalize(out var validated, out var errorDetail))
        {
            return Problem(
                title: "Invalid space payload",
                detail: errorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var created = await managementService.CreateAsync(validated!, cancellationToken);
            return Created($"/spaces/{created.Id}", created);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while creating a new space.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to create the space right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType<SpaceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateSpaceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > SpaceSearchQuery.MaxTextFilterLength)
        {
            return Problem(
                title: "Invalid space identifier",
                detail: $"The space identifier must be a non-empty string up to {SpaceSearchQuery.MaxTextFilterLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!request.TryValidateAndNormalize(out var validated, out var errorDetail))
        {
            return Problem(
                title: "Invalid space payload",
                detail: errorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var updated = await managementService.UpdateAsync(id, validated!, cancellationToken);
            if (updated is null)
            {
                return Problem(
                    title: "Space not found",
                    detail: $"Space '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(updated);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while updating space details.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to update the space right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("{id}/deactivate")]
    [ProducesResponseType<SpaceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Deactivate(
        [FromRoute] string id,
        CancellationToken cancellationToken) =>
        ChangeActiveStateAsync(id, isActive: false, cancellationToken);

    [HttpPost("{id}/activate")]
    [ProducesResponseType<SpaceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Activate(
        [FromRoute] string id,
        CancellationToken cancellationToken) =>
        ChangeActiveStateAsync(id, isActive: true, cancellationToken);

    private async Task<IActionResult> ChangeActiveStateAsync(
        string id,
        bool isActive,
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
            var updated = await managementService.SetActiveStateAsync(id, isActive, cancellationToken);
            if (updated is null)
            {
                return Problem(
                    title: "Space not found",
                    detail: $"Space '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(updated);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogWarning(ex, "MongoDB error while changing space active status.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to update space status right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
