using SmartCampus.Users.Api.Features.Registration;

namespace SmartCampus.Users.Api.Features.Login;

public record LoginResponse(
    string Token,
    string TokenType,
    int ExpiresIn,
    DateTime ExpiresAt,
    UserResponse User
);
