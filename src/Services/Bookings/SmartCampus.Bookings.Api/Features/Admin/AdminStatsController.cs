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
        DateOnly from, DateOnly to, StatsGroupBy groupBy, CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "'from' must not be after 'to'");
        }

        return Ok(await statsService.GetAsync(from, to, groupBy, cancellationToken));
    }
}
