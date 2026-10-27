namespace SmartCampus.Bookings.Api.Features.Bookings.Users;

public class UsersUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
