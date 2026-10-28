using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

[ApiController]
[Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
[Route("admin/alerts")]
public class AdminAlertsController(AdminAlertsReviewService alertsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<AdminAlertResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(AdminAlertStatus? status, CancellationToken cancellationToken)
    {
        return Ok(await alertsService.ListAsync(status, cancellationToken));
    }

    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType<AdminAlertResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(Guid id, ResolveAdminAlertRequest? request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            return Unauthorized();
        }

        var (alert, alreadyHandled) = await alertsService.ResolveAsync(id, adminId, request?.Note, cancellationToken);

        if (alert is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Alert not found");
        }

        if (alreadyHandled)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Alert already handled");
        }

        return Ok(alert);
    }
}
