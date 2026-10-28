using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Auth;

namespace SmartCampus.Bookings.Api.Features.Equipment;

[ApiController]
[Authorize]
[Route("bookings/{bookingId:guid}/equipment")]
public sealed class EquipmentRequestsController(
    IBookingEquipmentService equipmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EquipmentRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromRoute] Guid bookingId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.IsInRole(SmartCampusAuthConstants.RoleAdmin);

        var result = await equipmentService.GetByBookingIdAsync(
            bookingId,
            userId,
            email,
            isAdmin,
            cancellationToken);

        return MapResult(result, createdOnSuccess: false, bookingId);
    }

    [HttpPost]
    [ProducesResponseType<IReadOnlyList<EquipmentRequestResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromRoute] Guid bookingId,
        [FromBody] SubmitEquipmentRequestsPayload payload,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (User.IsInRole(SmartCampusAuthConstants.RoleAdmin))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Admins cannot request equipment",
                detail: "Only the organizer of a group booking can request equipment.");
        }

        var result = await equipmentService.AddToBookingAsync(
            bookingId,
            userId,
            payload,
            cancellationToken);

        return MapResult(result, createdOnSuccess: true, bookingId);
    }

    private IActionResult MapResult(EquipmentOperationResult result, bool createdOnSuccess, Guid bookingId)
    {
        return result.Outcome switch
        {
            EquipmentOperationOutcome.Succeeded => createdOnSuccess
                ? Created($"/bookings/{bookingId}/equipment", result.Items)
                : Ok(result.Items),
            EquipmentOperationOutcome.Invalid => ValidationProblem(
                new ValidationProblemDetails(result.ValidationErrors!.ToDictionary(e => e.Key, e => e.Value))),
            EquipmentOperationOutcome.BookingNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Booking not found",
                detail: result.Detail),
            EquipmentOperationOutcome.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: result.Detail),
            EquipmentOperationOutcome.BookingNotActive => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Booking not active",
                detail: result.Detail),
            EquipmentOperationOutcome.SpaceNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Space not found",
                detail: result.Detail),
            EquipmentOperationOutcome.SpacesUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Spaces unavailable",
                detail: result.Detail),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }
}
