using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Equipment.Models;

namespace SmartCampus.Spaces.Api.Features.Equipment;

[ApiController]
public sealed class SpaceEquipmentController(
    ISpaceEquipmentService equipmentService,
    ILogger<SpaceEquipmentController> logger) : ControllerBase
{
    [HttpGet("/equipment")]
    [HttpGet("/spaces/equipment")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<EquipmentCatalogItemResponse>>(StatusCodes.Status200OK)]
    public IActionResult GetCatalog()
    {
        return Ok(equipmentService.GetCatalog());
    }

    [HttpGet("/spaces/{id}/equipment")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<EquipmentCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBySpaceId(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(id))
        {
            return InvalidSpaceIdentifierProblem();
        }

        try
        {
            var items = await equipmentService.GetBySpaceIdAsync(id, cancellationToken);
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
            logger.LogWarning(ex, "MongoDB error while retrieving space equipment.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to retrieve space equipment right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPut("/spaces/{id}/equipment")]
    [Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
    [ProducesResponseType<SpaceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateSpaceEquipment(
        [FromRoute] string id,
        [FromBody] UpdateSpaceEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(id))
        {
            return InvalidSpaceIdentifierProblem();
        }

        if (!request.TryValidateAndNormalize(out var normalizedEquipment, out var errorDetail))
        {
            return Problem(
                title: "Invalid equipment payload",
                detail: errorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var updated = await equipmentService.UpdateSpaceEquipmentAsync(
                id,
                normalizedEquipment,
                cancellationToken);

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
            logger.LogWarning(ex, "MongoDB error while updating space equipment.");
            return Problem(
                title: "Spaces database unavailable",
                detail: "Unable to update space equipment right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= SpaceSearchQuery.MaxTextFilterLength;

    private ObjectResult InvalidSpaceIdentifierProblem() =>
        Problem(
            title: "Invalid space identifier",
            detail: $"The space identifier must be a non-empty string up to {SpaceSearchQuery.MaxTextFilterLength} characters.",
            statusCode: StatusCodes.Status400BadRequest);
}
