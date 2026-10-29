using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

[ApiController]
[Authorize]
[Route("invitations")]
public class InvitationsController(
    AcceptInvitationService acceptInvitationService,
    MyInvitationsService myInvitationsService) : ControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType<List<InvitationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);

        var invitations = await myInvitationsService.GetMineAsync(userId, email, cancellationToken);

        return Ok(invitations);
    }

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);

        var result = await acceptInvitationService.AcceptAsync(id, userId, email!, cancellationToken);

        return result.Outcome switch
        {
            AcceptInvitationOutcome.Accepted => Ok(InvitationResponse.From(
                result.Invitation!, result.ParticipantCount)),
            AcceptInvitationOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Invitation not found",
                detail: "No pending invitation for this user with that id."),
            AcceptInvitationOutcome.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Not your invitation",
                detail: "The invitation was issued to another user."),
            AcceptInvitationOutcome.Expired => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Invitation no longer pending",
                detail: "The invitation was already responded to or its deadline has passed."),
            AcceptInvitationOutcome.TimeBandAlreadyUsed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Time band already used",
                detail: "You already have a confirmed booking in that time band on that day."),
            AcceptInvitationOutcome.PersonConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Overlapping booking",
                detail: "You already have a confirmed booking that overlaps this time band."),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
        };
    }
}
