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
    public async Task<IActionResult> GetUsers([FromQuery] string? emails, CancellationToken cancellationToken)
    {
        var query = dbContext.UserProfiles
            .AsNoTracking()
            .Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(emails))
        {
            var emailList = emails
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.ToLowerInvariant())
                .Distinct()
                .ToList();

            query = query.Where(u => emailList.Contains(u.Email));
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        return Ok(users.Select(UserResponse.FromEntity));
    }
}
