using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Auth;

namespace SmartCampus.Bookings.Api.Features.Admin;

[ApiController]
[Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
[Route("admin/stats")]
public class AdminStatsController(AdminStatsService statsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<StatsGroupResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        DateOnly? from, DateOnly? to, StatsGroupBy groupBy, CancellationToken cancellationToken)
    {
        if (from is null || to is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "'from' and 'to' query parameters are required");
        }

        if (from > to)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "'from' must not be after 'to'");
        }

        return Ok(await statsService.GetAsync(from.Value, to.Value, groupBy, cancellationToken));
    }
}
