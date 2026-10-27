namespace SmartCampus.Users.Api.Features.Registration;

public record RegisterRequest(
    string Matricola,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Password
);
