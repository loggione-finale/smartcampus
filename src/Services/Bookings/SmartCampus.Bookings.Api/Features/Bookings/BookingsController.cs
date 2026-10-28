using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Api.Features.Bookings.MyBookings;

namespace SmartCampus.Bookings.Api.Features.Bookings;

[ApiController]
[Authorize]
[Route("bookings")]
public class BookingsController(
    BookingService bookingService,
    CancelBookingService cancelBookingService,
    MyBookingsService myBookingsService,
    IClosingCommentService closingCommentService) : ControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType<List<MyBookingResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.Email);

        var bookings = await myBookingsService.GetMineAsync(userId, email, cancellationToken);
        return Ok(bookings);
    }

    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var organizerId))
        {
            return Unauthorized();
        }

        var organizerName = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? organizerId.ToString();

        var organizerEmail = User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.Email);
        if (organizerEmail is null)
        {
            return Unauthorized();
        }

        var result = await bookingService.CreateAsync(
            organizerId, organizerName, organizerEmail, request, cancellationToken);

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
            CreateBookingOutcome.TimeBandAlreadyUsed => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Time band already used", detail: result.Detail),
            CreateBookingOutcome.NoSeatsLeft => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Not enough seats",
                detail: result.Detail,
                extensions: new Dictionary<string, object?> { ["freeSeats"] = result.FreeSeats }),
            CreateBookingOutcome.GroupTooLarge => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Group too large",
                detail: result.Detail,
                extensions: new Dictionary<string, object?> { ["maxGroupSize"] = result.MaxGroupSize }),
            CreateBookingOutcome.SpacesUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable, title: "Spaces unavailable", detail: result.Detail),
            CreateBookingOutcome.UsersUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable, title: "Users unavailable", detail: result.Detail),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid id, CancelBookingRequest? request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await cancelBookingService.CancelAsync(id, userId, request?.Reason, cancellationToken);

        return result.Outcome switch
        {
            CancelBookingOutcome.Cancelled => Ok(BookingResponse.From(result.Booking!)),
            CancelBookingOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Booking not found"),
            CancelBookingOutcome.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Only the organizer can cancel a booking"),
            CancelBookingOutcome.AlreadyCancelled => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Booking already cancelled"),
            CancelBookingOutcome.AlreadyEnded => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Booking already ended",
                detail: "A booking that has ended cannot be cancelled."),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }

    [HttpPost("{code}/closing-comment")]
    [ProducesResponseType<ClosingCommentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitClosingComment(
        string code,
        SubmitClosingCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await closingCommentService.SubmitAsync(code, userId, request, cancellationToken);

        return result.Outcome switch
        {
            ClosingCommentOutcome.Success => Ok(ClosingCommentResponse.From(result.Comment!, result.Booking!)),
            ClosingCommentOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Booking not found",
                detail: result.Detail),
            ClosingCommentOutcome.NotEnded => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Booking has not ended yet",
                detail: result.Detail),
            ClosingCommentOutcome.AlreadyCancelled => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Booking already cancelled",
                detail: result.Detail),
            ClosingCommentOutcome.AlreadyClosed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Closing comment already submitted",
                detail: result.Detail),
            ClosingCommentOutcome.Invalid => ValidationProblem(
                new ValidationProblemDetails(result.ValidationErrors!.ToDictionary(e => e.Key, e => e.Value))),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }
}
