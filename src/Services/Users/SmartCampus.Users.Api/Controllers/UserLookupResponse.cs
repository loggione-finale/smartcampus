using SmartCampus.Users.Api.Domain.Entities;

namespace SmartCampus.Users.Api.Controllers;

/// <summary>
/// Public view of a user for lookups between services: no phone number or matricola,
/// because these endpoints are reachable without authentication (#261).
/// </summary>
public record UserLookupResponse(Guid Id, string Email, string DisplayName)
{
    public static UserLookupResponse FromEntity(UserProfile user) =>
        new(user.Id, user.Email, user.DisplayName);
}
