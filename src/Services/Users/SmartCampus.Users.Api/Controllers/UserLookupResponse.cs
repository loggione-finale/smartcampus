using SmartCampus.Users.Api.Domain.Entities;

namespace SmartCampus.Users.Api.Controllers;

public record UserLookupResponse(
    Guid Id,
    string Email,
    string DisplayName
)
{
    public static UserLookupResponse FromEntity(UserProfile user) =>
        new(user.Id, user.Email, user.DisplayName);
}
