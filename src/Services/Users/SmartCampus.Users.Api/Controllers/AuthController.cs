using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Auth;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;
using SmartCampus.Users.Api.Features.Login;
using SmartCampus.Users.Api.Features.Registration;

namespace SmartCampus.Users.Api.Controllers;

[ApiController]
[Route("auth")]
[Route("api/auth")]
public class AuthController(
    UsersDbContext dbContext,
    IPasswordHasher<UserProfile> passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
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

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken)
    {
        var identifier = request?.GetIdentifier();
        if (request is null || string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Failed",
                Detail = "Username (email or matricola) and password are required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var normalizedEmail = identifier.ToLowerInvariant();
        var user = await dbContext.UserProfiles
            .FirstOrDefaultAsync(u => (u.Email == normalizedEmail || u.Matricola == identifier) && u.IsActive, cancellationToken);

        if (user is null)
        {
            var logSafeIdentifier = identifier.Replace("\r", string.Empty).Replace("\n", string.Empty);
            logger.LogWarning("Login failed for identifier '{Identifier}': user not found or inactive", logSafeIdentifier);
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid credentials.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Login failed for user '{UserId}': invalid password", user.Id);
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid credentials.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var signingKey = configuration[SmartCampusAuthConstants.DefaultSigningKeyEnvVar]
            ?? configuration[SmartCampusAuthConstants.DefaultSigningKeyConfigKey]
            ?? (environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? "development-super-secret-jwt-signing-key-for-smartcampus-123456"
                : throw new InvalidOperationException("Missing JWT signing key. Set 'JWT_SECRET' in environment or user-secrets."));

        var now = DateTime.UtcNow;
        var lifetime = JwtTokenHelper.DefaultTokenLifetime; // 8 hours
        var expiresAt = now.Add(lifetime);

        var additionalClaims = new List<Claim>
        {
            new("matricola", user.Matricola),
            new("display_name", user.DisplayName),
            new(JwtRegisteredClaimNames.Name, user.DisplayName)
        };

        var token = JwtTokenHelper.GenerateToken(
            userId: user.Id.ToString(),
            email: user.Email,
            role: user.Role,
            signingKey: signingKey,
            lifetime: lifetime,
            additionalClaims: additionalClaims);

        logger.LogInformation("User '{UserId}' successfully logged in", user.Id);

        return Ok(new LoginResponse(
            Token: token,
            TokenType: "Bearer",
            ExpiresIn: (int)lifetime.TotalSeconds,
            ExpiresAt: expiresAt,
            User: UserResponse.FromEntity(user)
        ));
    }
}
