using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartCampus.Bookings.Api.Features.Bookings;

[ApiController]
[Authorize]
[Route("bookings")]
public class BookingsController(BookingService bookingService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var organizerId))
        {
            return Unauthorized();
        }

        var organizerName = User.FindFirstValue(ClaimTypes.Email) ?? userIdClaim;

        var result = await bookingService.CreateAsync(organizerId, organizerName, request, cancellationToken);

        return result.Outcome switch
        {
            CreateBookingOutcome.Created => Created(
                $"/bookings/{result.Booking!.Id}", BookingResponse.From(result.Booking!)),
            CreateBookingOutcome.Invalid => ValidationProblem(
                new ValidationProblemDetails(result.ValidationErrors!.ToDictionary(e => e.Key, e => e.Value))),
            CreateBookingOutcome.PendingClosure => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Closing comment pending",
                detail: result.Detail,
                extensions: new Dictionary<string, object?> { ["bookingToComment"] = result.PendingReferenceCode }),
            CreateBookingOutcome.SpaceNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Space not found", detail: result.Detail),
            CreateBookingOutcome.SpaceNotBookable => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Space not bookable", detail: result.Detail),
            CreateBookingOutcome.SpaceConflict => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Space already booked", detail: result.Detail),
            CreateBookingOutcome.PersonConflict => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Overlapping booking", detail: result.Detail),
            CreateBookingOutcome.SpacesUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable, title: "Spaces unavailable", detail: result.Detail),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }
}
