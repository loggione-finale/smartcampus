using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Features.Registration;

namespace SmartCampus.Users.Api.Controllers;

[ApiController]
[Route("users")]
[Route("api/users")]
public class UsersController(UsersDbContext dbContext) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetById))]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(UserResponse.FromEntity(user));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUsers([FromQuery] string? emails, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(emails))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "The 'emails' query parameter is required for user lookup.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var emailList = emails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (emailList.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "The 'emails' query parameter cannot be empty.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var users = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(u => u.IsActive && emailList.Contains(u.Email))
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        return Ok(users.Select(UserResponse.FromEntity));
    }
}
