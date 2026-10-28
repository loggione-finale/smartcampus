using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Auth;

namespace SmartCampus.Bookings.Api.Features.Admin;

[ApiController]
[Authorize(Policy = SmartCampusAuthConstants.PolicyAdmin)]
[Route("admin")]
public class AdminController(AdminDashboardService dashboardService) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType<List<DashboardSpaceResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(
        DateOnly? date, string? building, string? type, CancellationToken cancellationToken)
    {
        return Ok(await dashboardService.GetAsync(date, building, type, cancellationToken));
    }
}
