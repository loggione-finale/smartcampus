using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;
using SmartCampus.Users.Api.Features.Registration;

namespace SmartCampus.Users.Api.Controllers;

[ApiController]
[Route("auth")]
[Route("api/auth")]
public class AuthController(UsersDbContext dbContext, IPasswordHasher<UserProfile> passwordHasher, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = RegisterValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(validationErrors)
            {
                Title = "Validation Failed",
                Detail = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedMatricola = request.Matricola.Trim();

        if (await dbContext.UserProfiles.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "Email is already registered.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (await dbContext.UserProfiles.AnyAsync(u => u.Matricola == normalizedMatricola, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "Matricola is already registered.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            Matricola = normalizedMatricola,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = $"{firstName} {lastName}",
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            Role = UserRole.User, // Self-registration is restricted to the User role only
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Note: Password is never logged
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.UserProfiles.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var logSafeMatricola = user.Matricola.Replace("\r", string.Empty).Replace("\n", string.Empty);
        logger.LogInformation("User {UserId} with matricola {Matricola} successfully registered", user.Id, logSafeMatricola);

        var response = UserResponse.FromEntity(user);
        return CreatedAtAction(nameof(UsersController.GetById), "Users", new { id = user.Id }, response);
    }
}
