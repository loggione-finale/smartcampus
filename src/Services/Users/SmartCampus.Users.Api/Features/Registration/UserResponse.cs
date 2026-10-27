using SmartCampus.Users.Api.Domain.Entities;

namespace SmartCampus.Users.Api.Features.Registration;

public record UserResponse(
    Guid Id,
    string Matricola,
    string FirstName,
    string LastName,
    string DisplayName,
    string Email,
    string PhoneNumber,
    string Role,
    DateTime CreatedAt
)
{
    public static UserResponse FromEntity(UserProfile user) =>
        new(
            user.Id,
            user.Matricola,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.Email,
            user.PhoneNumber,
            user.Role,
            user.CreatedAt
        );
}
