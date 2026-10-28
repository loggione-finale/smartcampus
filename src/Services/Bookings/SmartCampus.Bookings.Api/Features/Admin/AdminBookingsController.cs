using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Features.Bookings;

namespace SmartCampus.Bookings.Api.Features.Admin;

public record AdminCancelBookingRequest([Required, StringLength(500)] string Reason);

[ApiController]
[Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
[Route("admin/bookings")]
public class AdminBookingsController(AdminCancelBookingService cancelService) : ControllerBase
{
    [HttpPost("{code}/cancel")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(string code, AdminCancelBookingRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            return Unauthorized();
        }

        var result = await cancelService.CancelAsync(code, adminId, request.Reason, cancellationToken);

        return result.Outcome switch
        {
            CancelBookingOutcome.Cancelled => Ok(BookingResponse.From(result.Booking!)),
            CancelBookingOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Booking not found"),
            CancelBookingOutcome.AlreadyCancelled => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Booking already cancelled"),
            CancelBookingOutcome.AlreadyEnded => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Booking already ended",
                detail: "A booking that has ended cannot be cancelled."),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }
}
